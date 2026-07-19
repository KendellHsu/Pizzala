// ─────────────────────────────────────────────────────────────
// BossCommentService.cs — calls Gemini to write the boss's post-shift note.
// (Every player now sees the boss note page - condition no longer gates this.)
// Attach to the same object as GameManager.
//
// API key: read at runtime from Assets/StreamingAssets/gemini_api_key.txt
// (gitignored - each machine supplies its own, get a free one at
// aistudio.google.com). Loaded via UnityWebRequest so it also works on
// Android/Quest builds, not just the Editor/Standalone file system.
//
// Network call is async (coroutine) - never blocks EndRound(). If the key
// is missing or the request fails/times out, GetComment() falls back to a
// canned comment so the experiment never gets stuck on a blank note.
// ─────────────────────────────────────────────────────────────
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Pizzala.Data;

namespace Pizzala.LLM
{
    public class BossCommentService : MonoBehaviour
    {
        const string Model = "gemini-flash-latest";
        const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/" + Model + ":generateContent";
        const string ApiKeyFileName = "gemini_api_key.txt";
        // 20s + one retry: the free tier regularly 503s or just sits there when Google's
        // side is busy - nothing wrong on ours. The player is reading pages 1-2 while this
        // runs, so waiting longer costs nothing; the note page shows "writing..." meanwhile.
        const int TimeoutSeconds = 20;
        const int MaxAttempts = 2;

        // Accuracy is genuinely hard in VR and most real playtests land near 0% - the
        // note should never mock that number itself. Jokes land on specific incidents
        // (face hits, mess count) instead, which is why the summary block below leads
        // with those rather than accuracy.
        // Line 3 (persona) is stripped before the note is shown in-game - it exists purely
        // for the history website's player card, riding along on the same call for free.
        const string PromptTemplate =
            "You are the boss of a pizza shop. An employee (the player) just finished a shift. " +
            "Here is tonight's performance data:\n\n{0}\n\n" +
            "Write the short note the boss leaves for them after the shift, in English.\n" +
            "Format - exactly three lines:\n" +
            "Line 1: two playful hashtags that sum up the shift, each a simple adjective or a " +
            "1-3 word phrase (e.g. #SaucyChaos #BigEffort).\n" +
            "Line 2: the note itself, 25-40 words.\n" +
            "Line 3: a 2-4 word player persona title, like a playful character class based on " +
            "how they played (e.g. Sauce Tornado, Zen Delivery Master). No hashtag, no quotes.\n" +
            "Tone: mostly encouraging, with one funny roast about a specific mishap (like " +
            "hitting a customer in the face or making a mess) - not about the accuracy number, " +
            "since accurate throws are genuinely hard and most people miss a lot; that's normal, " +
            "not a personal failing. End positive so they want another shift. Handwritten-note " +
            "style, no lists. Output only those three lines, no titles or explanations.";

        string apiKey;
        bool apiKeyLoadAttempted;

        // onComplete(comment, persona): comment = hashtags line + note line (what the
        // in-game sticky note shows); persona = the website-only class title, already
        // split off so no caller ever accidentally renders it.
        public void GetComment(SessionSummary summary, Action<string, string> onComplete)
        {
            StartCoroutine(GetCommentRoutine(summary, onComplete));
        }

        IEnumerator GetCommentRoutine(SessionSummary summary, Action<string, string> onComplete)
        {
            if (!apiKeyLoadAttempted)
                yield return LoadApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[BossCommentService] No API key found at StreamingAssets/" + ApiKeyFileName + " - using fallback comment.");
                Deliver(FallbackComment(summary), summary, onComplete);
                yield break;
            }

            string prompt = string.Format(PromptTemplate, BuildStatsBlock(summary));
            string body = JsonUtility.ToJson(new GeminiRequest
            {
                contents = new[] { new GeminiContent { parts = new[] { new GeminiPart { text = prompt } } } }
            });

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                using (var req = new UnityWebRequest(Endpoint, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("x-goog-api-key", apiKey);
                    req.timeout = TimeoutSeconds;

                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        string text = ExtractText(req.downloadHandler.text);
                        Deliver(string.IsNullOrEmpty(text) ? FallbackComment(summary) : text.Trim(),
                                summary, onComplete);
                        yield break;
                    }

                    Debug.LogWarning($"[BossCommentService] Gemini request failed ({req.error}) - " +
                                     (attempt < MaxAttempts ? "retrying." : "using fallback comment."));
                }
            }

            Deliver(FallbackComment(summary), summary, onComplete);
        }

        // Splits the raw three-line response into (note for the game, persona for the web).
        // Tolerant of the LLM misbehaving: 3+ lines -> last line is the persona (even if
        // the note wrapped onto extra lines); 2 or fewer (incl. every canned fallback) ->
        // whole text is the note and the persona comes from the canned pools instead.
        static void Deliver(string raw, SessionSummary summary, Action<string, string> onComplete)
        {
            var lines = new System.Collections.Generic.List<string>();
            foreach (var l in raw.Split('\n'))
                if (!string.IsNullOrWhiteSpace(l)) lines.Add(l.Trim());

            string comment, persona;
            if (lines.Count >= 3)
            {
                persona = lines[lines.Count - 1].TrimStart('#').Trim();
                lines.RemoveAt(lines.Count - 1);
                comment = string.Join("\n", lines);
            }
            else
            {
                comment = string.Join("\n", lines);
                persona = FallbackPersona(summary);
            }
            onComplete?.Invoke(comment, persona);
        }

        IEnumerator LoadApiKey()
        {
            apiKeyLoadAttempted = true;
            string path = Path.Combine(Application.streamingAssetsPath, ApiKeyFileName);

            using (var req = UnityWebRequest.Get(path))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                    apiKey = req.downloadHandler.text.Trim();
            }
        }

        // Only the fields we actually read - the rest of the response is ignored.
        [Serializable] class GeminiPart { public string text; }
        [Serializable] class GeminiContent { public GeminiPart[] parts; }
        [Serializable] class GeminiRequest { public GeminiContent[] contents; }
        [Serializable] class GeminiCandidate { public GeminiContent content; }
        [Serializable] class GeminiResponse { public GeminiCandidate[] candidates; }

        static string ExtractText(string json)
        {
            try
            {
                var response = JsonUtility.FromJson<GeminiResponse>(json);
                return response?.candidates != null && response.candidates.Length > 0
                    ? response.candidates[0].content.parts[0].text
                    : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BossCommentService] Couldn't parse Gemini response: {e.Message}");
                return null;
            }
        }

        static string BuildStatsBlock(SessionSummary s)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine($"- Threw {s.totalThrows} times, hit {s.hits} ({s.accuracyPercent.ToString("F0", inv)}%)");
            sb.AppendLine($"- Made a mess in {s.dirtCount} spots");
            sb.AppendLine($"- Missed {s.missedOrders} orders");
            if (s.playerFaceHits > 0)
                sb.AppendLine($"- Got hit back in the face {s.playerFaceHits} time(s)");
            if (s.dodgeTotal > 0)
                sb.AppendLine($"- Dodged {s.dodgeSuccess}/{s.dodgeTotal} pizzas thrown back");
            sb.AppendLine($"- Moved {s.totalHeadDistance.ToString("F0", inv)}m, squatted {s.squatCount} times");
            return sb.ToString();
        }

        // Same situation buckets as before (big mess / got pizza'd in the face / otherwise),
        // but each picks randomly from a pool so back-to-back rounds don't read the exact
        // same note - the fallback fires often enough (free-tier Gemini flakiness) that
        // repeat players would notice a single canned line.
        // Same two-line shape as the LLM output (hashtags, then the note) so the fallback
        // is indistinguishable from a real response on the note UI.
        static readonly string[] MessyComments =
        {
            "#SauceStorm #StillStanding\nRough one, but you showed up and threw pizza - that's the job. Clean up before your next shift.",
            "#AbstractArt #GoodHustle\nThe shop looks like a sauce crime scene. Mop's in the back. Still, good hustle - see you tomorrow.",
            "#SplatCount #RestUp\nI counted the splats. All of them. We'll talk aim later - for now, rest up. You earned it.",
            "#HealthHazard #ComebackKid\nThe inspector called; I said it was 'abstract art'. Aim for their HANDS next shift. You've got this.",
        };

        static readonly string[] FaceHitComments =
        {
            "#SauceFace #BattleScars\nTook one right in the face tonight. Occupational hazard. You wear it well - see you next shift.",
            "#PizzaKarma #RoundTwo\nRule one of pizza: sometimes it comes back. You learned that the hard way. Come back for round two.",
            "#QuickReflexes #AlmostDodged\nThat customer had quite an arm, huh? Keep your head moving. Otherwise, decent shift - see you tomorrow.",
        };

        static readonly string[] DefaultComments =
        {
            "#SolidEffort #KeepAtIt\nGood work out there tonight. You'll get the hang of it. See you next shift.",
            "#NightsWork #HappyRegulars\nNot bad at all. The regulars were smiling - that's what counts. Same time tomorrow?",
            "#FindingRhythm #GoodShift\nYou're getting the rhythm of this place. A few wild throws, sure, but everyone starts somewhere.",
            "#TillCounted #CallItAWin\nOvens cooling, till counted, nobody quit - I call that a win. See you next shift.",
        };

        // Public entry so GameManager's else branch (service present but we still want a
        // guaranteed note without a network round-trip) can borrow the same canned pool.
        public static string GetFallbackComment(SessionSummary s) => FallbackComment(s);

        static string FallbackComment(SessionSummary s)
        {
            string[] pool = s.dirtCount > 10 ? MessyComments
                          : s.playerFaceHits > 0 ? FaceHitComments
                          : DefaultComments;
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        // Canned personas, same buckets as the comments. Used whenever the LLM response
        // didn't include a line 3 (or the whole call fell back).
        static readonly string[] MessyPersonas = { "Sauce Tornado", "Abstract Artist", "Splat Machine", "Chaos Chef" };
        static readonly string[] FaceHitPersonas = { "Sauce Magnet", "Fearless Frontliner", "Human Dartboard" };
        static readonly string[] DefaultPersonas = { "Steady Hand", "Night Shift Regular", "Rising Rookie", "Honest Worker" };

        static string FallbackPersona(SessionSummary s)
        {
            string[] pool = s.dirtCount > 10 ? MessyPersonas
                          : s.playerFaceHits > 0 ? FaceHitPersonas
                          : DefaultPersonas;
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
    }
}
