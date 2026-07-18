// ─────────────────────────────────────────────────────────────
// PlayerCardService.cs — generates the clay-style "player card" portrait for the history
// website. Called from GameManager.EndRound(); the game itself never shows this image
// (the results screen's player photo slot was removed), so the 5-20s generation time is
// invisible - it lands in photos/ whenever it lands and the JSON gets re-saved.
//
// How the image is made: Gemini's image model gets our two style reference photos
// (clean chef / sauce-drenched chef, in StreamingAssets/style_refs/) plus a prompt built
// from the round's stats - dirtCount and face hits decide how much sauce ends up on the
// character. Same API key file as BossCommentService.
//
// Failure never leaves an empty polaroid on the wall: a random pre-made card from
// StreamingAssets/fallback_cards/ is copied into photos/ instead (art team supplies
// those; until they exist, playerCardImage stays "" and the site shows its placeholder).
//
// Mirrors BossCommentService's patterns on purpose (coroutine, key loading, retry,
// canned fallback) - one mental model for every LLM call in the project.
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
    public class PlayerCardService : MonoBehaviour
    {
        // Image models on the free tier are limit:0 (blocked) - this needs a billing-enabled
        // key. gemini-3-pro-image is the strongest; drop to gemini-3.1-flash-image for a
        // cheaper/faster option. Exposed so it can be swapped in the Inspector while iterating.
        [Tooltip("Image model id. Needs a paid (billing-enabled) API key - free tier is blocked for all image models.")]
        public string model = "gemini-3-pro-image";
        const string ApiKeyFileName = "gemini_api_key.txt";
        string Endpoint => $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        // Image generation is slower than text - give it more rope than the comment call.
        const int TimeoutSeconds = 45;
        const int MaxAttempts = 2;

        // Paths (under StreamingAssets/) of the style anchor images. Keep this to a few -
        // 1-2 clean + 1 saucy is plenty; every extra image inflates the request and can
        // over-anchor the output. Missing files are skipped with a warning. Inspector-
        // editable so the art refs can be swapped without touching code.
        [Tooltip("Style reference images under StreamingAssets/. A couple is plenty - too many bloats the request.")]
        public string[] styleRefFiles =
        {
            "style_refs/Clean1.png",
            "style_refs/Saucy.png",
        };
        const string FallbackCardsDir = "fallback_cards"; // under StreamingAssets, PNGs

        // The reference images kept bleeding the SAME character (the cap-and-turtleneck guy)
        // into every result. The fix is a hard style-vs-content split plus a randomized
        // "character seed" ({2}) that forces a genuinely different person each time - the
        // refs are explicitly demoted to texture/lighting only.
        const string PromptTemplate =
            "Copy ONLY the ART STYLE of the attached images: hand-sculpted claymation / " +
            "stop-motion clay texture, soft studio lighting, plain light dotted background, " +
            "full body, front view, single character centered.\n" +
            "Do NOT reuse the reference character. Their face, gender, age, hair, hat and " +
            "clothing are NOT to be copied. Invent a COMPLETELY DIFFERENT new person.\n\n" +
            "The new pizza-shop employee: {2}\n\n" +
            "They just finished a shift with this performance:\n{0}\n" +
            "Reflect the shift on the character: {1}\n" +
            "Clay style only, one original character, no text or logos in the image.";

        string apiKey;
        bool apiKeyLoadAttempted;

        /// <summary>
        /// Generates the card, writes the PNG to persistentDataPath/photos/, sets
        /// session.playerCardImage, then calls onDone (hook SaveToDisk there).
        /// onDone always fires exactly once, fallback or not.
        /// </summary>
        public void GenerateCard(SessionData session, Action onDone)
        {
            StartCoroutine(GenerateRoutine(session, onDone));
        }

        IEnumerator GenerateRoutine(SessionData session, Action onDone)
        {
            if (!apiKeyLoadAttempted)
                yield return LoadApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[PlayerCardService] No API key at StreamingAssets/" + ApiKeyFileName + " - using fallback card.");
                UseFallbackCard(session);
                onDone?.Invoke();
                yield break;
            }

            // Style refs load via UnityWebRequest so this also works inside a Quest build,
            // where StreamingAssets lives in the apk and File.Read can't reach it.
            var refImages = new System.Collections.Generic.List<byte[]>();
            foreach (var rel in styleRefFiles)
            {
                string path = Path.Combine(Application.streamingAssetsPath, rel);
                using (var req = UnityWebRequest.Get(path))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                        refImages.Add(req.downloadHandler.data);
                    else
                        Debug.LogWarning($"[PlayerCardService] Style ref missing: {rel} - continuing without it.");
                }
            }

            string body = BuildRequestJson(session.summary, refImages);

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                using (var req = new UnityWebRequest(Endpoint, "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("x-goog-api-key", apiKey);
                    req.timeout = TimeoutSeconds;

                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        byte[] png = ExtractImage(req.downloadHandler.text);
                        if (png != null)
                        {
                            SaveCard(session, png);
                            onDone?.Invoke();
                            yield break;
                        }
                        Debug.LogWarning("[PlayerCardService] Response had no image part - " +
                                         (attempt < MaxAttempts ? "retrying." : "using fallback card."));
                    }
                    else
                    {
                        // Body often carries the actual reason (bad field name, quota...) -
                        // the status line alone is useless for diagnosing 400s.
                        Debug.LogWarning($"[PlayerCardService] Request failed ({req.error}) - " +
                                         (attempt < MaxAttempts ? "retrying." : "using fallback card.") +
                                         $"\n{Truncate(req.downloadHandler.text, 500)}");
                    }
                }
            }

            UseFallbackCard(session);
            onDone?.Invoke();
        }

        // ── prompt & request ──

        // dirtCount / face hits decide how sauced-up the character is, mapping onto the
        // two reference photos' clean-to-drenched range.
        static string DescribeAppearance(SessionSummary s)
        {
            string sauce = s.dirtCount > 10
                ? "completely covered in dripping pizza sauce and toppings from head to toe, like the messier reference"
                : s.dirtCount > 3
                    ? "noticeably splattered with pizza sauce on the apron, arms and one cheek"
                    : "mostly clean, just a small proud smudge of sauce somewhere";

            string face = s.playerFaceHits > 0
                ? $" They took {s.playerFaceHits} pizza(s) to the face, so add a big comical sauce splat on the face."
                : "";

            string mood = s.hits >= 5
                ? " Confident, proud pose - this was a great shift."
                : " Tired but happy, endearing underdog energy.";

            return sauce + "." + face + mood;
        }

        // A fresh randomized persona each call so the model can't fall back on the
        // reference character - the strongest lever against "everyone looks the same".
        // Purely cosmetic variety; unrelated to the LLM-written playerPersona title.
        static readonly string[] Genders = { "man", "woman", "person" };
        static readonly string[] Ages = { "young", "middle-aged", "older", "twenty-something", "teenage" };
        static readonly string[] Builds = { "tall and lanky", "short and round", "stocky", "slim", "average build" };
        static readonly string[] Hair = { "curly hair", "a shaved head", "a messy bun", "long straight hair",
                                          "an afro", "a buzz cut and a beard", "braided hair", "spiky hair, wearing glasses" };
        static readonly string[] Skin = { "dark skin", "light skin", "olive skin", "brown skin", "tan skin" };
        static readonly string[] Outfits = { "a red apron over a striped shirt", "a blue polo and cap",
                                            "a green hoodie", "a yellow t-shirt and apron", "a checkered shirt",
                                            "a denim jacket over an apron", "a purple sweater" };

        static string RandomCharacterSeed()
        {
            string Pick(string[] a) => a[UnityEngine.Random.Range(0, a.Length)];
            return $"a {Pick(Ages)} {Pick(Genders)} with {Pick(Skin)}, {Pick(Builds)}, {Pick(Hair)}, wearing {Pick(Outfits)}";
        }

        static string StatsBlock(SessionSummary s)
        {
            var inv = CultureInfo.InvariantCulture;
            return $"- {s.hits} successful deliveries out of {s.totalThrows} throws\n" +
                   $"- Made a mess in {s.dirtCount} spots\n" +
                   $"- Hit in the face {s.playerFaceHits} time(s)\n" +
                   $"- Moved {s.totalHeadDistance.ToString("F0", inv)}m during the shift\n";
        }

        // Hand-assembled JSON: JsonUtility can't express "a part has EITHER text OR
        // inlineData" (it serialises every field, and the API rejects hybrid parts).
        // Base64 needs no escaping, and the prompt is escaped below, so this is safe.
        static string BuildRequestJson(SessionSummary summary, System.Collections.Generic.List<byte[]> refImages)
        {
            string prompt = string.Format(PromptTemplate, StatsBlock(summary), DescribeAppearance(summary), RandomCharacterSeed());
            var sb = new StringBuilder();
            sb.Append("{\"contents\":[{\"parts\":[");
            foreach (var img in refImages)
                sb.Append("{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"")
                  .Append(Convert.ToBase64String(img))
                  .Append("\"}},");
            sb.Append("{\"text\":\"").Append(EscapeJson(prompt)).Append("\"}");
            // responseModalities IMAGE is required for image output - without it these
            // models can reply with only text. If a paid key still yields no image, try
            // ["TEXT","IMAGE"] here.
            sb.Append("]}],\"generationConfig\":{\"responseModalities\":[\"IMAGE\"]}}");
            return sb.ToString();
        }

        static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

        // ── response ──

        [Serializable] class GeminiInlineData { public string mimeType; public string data; }
        [Serializable] class GeminiPart { public string text; public GeminiInlineData inlineData; }
        [Serializable] class GeminiContent { public GeminiPart[] parts; }
        [Serializable] class GeminiCandidate { public GeminiContent content; }
        [Serializable] class GeminiResponse { public GeminiCandidate[] candidates; }

        static byte[] ExtractImage(string json)
        {
            try
            {
                var response = JsonUtility.FromJson<GeminiResponse>(json);
                if (response?.candidates == null || response.candidates.Length == 0) return null;
                foreach (var part in response.candidates[0].content.parts)
                    if (part.inlineData != null && !string.IsNullOrEmpty(part.inlineData.data))
                        return Convert.FromBase64String(part.inlineData.data);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerCardService] Couldn't parse image response: {e.Message}");
                return null;
            }
        }

        // ── output ──

        void SaveCard(SessionData session, byte[] png)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "photos");
                Directory.CreateDirectory(dir);
                string fileName = $"card_{session.sessionId}.png";
                File.WriteAllBytes(Path.Combine(dir, fileName), png);
                session.playerCardImage = fileName;
                Debug.Log($"[PlayerCardService] Player card saved: photos/{fileName}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerCardService] Failed to save card: {e.Message}");
                UseFallbackCard(session);
            }
        }

        // Copies a random pre-made card into photos/ under this session's name, so the
        // website needs no special "generation failed" handling - there is always a file.
        void UseFallbackCard(SessionData session)
        {
            try
            {
                string dir = Path.Combine(Application.streamingAssetsPath, FallbackCardsDir);
                // Editor/Windows only reach: on Quest, StreamingAssets isn't a listable
                // directory - acceptable for now since generation is the normal path there.
                if (!Directory.Exists(dir)) { Warn(); return; }
                var cards = Directory.GetFiles(dir, "*.png");
                if (cards.Length == 0) { Warn(); return; }

                var pick = cards[UnityEngine.Random.Range(0, cards.Length)];
                string outDir = Path.Combine(Application.persistentDataPath, "photos");
                Directory.CreateDirectory(outDir);
                string fileName = $"card_{session.sessionId}.png";
                File.Copy(pick, Path.Combine(outDir, fileName), true);
                session.playerCardImage = fileName;
                Debug.Log($"[PlayerCardService] Fallback card used: {Path.GetFileName(pick)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerCardService] Fallback card failed too: {e.Message}");
            }

            void Warn() => Debug.LogWarning(
                "[PlayerCardService] No fallback cards in StreamingAssets/" + FallbackCardsDir +
                " - playerCardImage stays empty, the website will show its placeholder.");
        }

        static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "...");

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
    }
}
