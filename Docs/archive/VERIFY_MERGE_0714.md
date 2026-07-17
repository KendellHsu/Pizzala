# åˆä½µé©—æ”¶æ¸…å–® â€” 2026-07-14 yenchia ç¾Žè¡“è³‡ç”¢

> 2026-07-15 æ›´æ–°:ä¸‹è¡¨åˆ—çš„è·¯å¾‘æ˜¯åˆä½µç•¶ä¸‹çš„ä½ç½®,ç¾åœ¨å·²ä¾é¡žåž‹åˆ†å­è³‡æ–™å¤¾(è¦‹ [PREFABS.md](../PREFABS.md))â€”â€”
> æŠ«è–©/ä¸Ÿå›žæŠ«è–© â†’ `Assets/Prefabs/Pizza/`ã€é†¬æ± â†’ `Assets/Prefabs/SauceSplat/`ã€‚GUID æ²’è®Š,å ´æ™¯å¼•ç”¨ä¸å—å½±éŸ¿ã€‚

> ç”¨æ³•:åœ¨ Unity è£¡ç…§é †åºé€é …æ‰“å‹¾,å…¨éŽå¾Œå›žå ± Claude åŸ·è¡Œ pushã€‚
> é‡åˆ°ä¸ç¬¦çš„é …ç›®**å…ˆä¸è¦ä¿®**,æˆªåœ–/è¨˜ä¸‹ä¾†å›žå ±,ç¢ºèªåŽŸå› å†å‹•æ‰‹ã€‚
> ä¸€èˆ¬æ€§çš„åˆä½µæµç¨‹è¦‹ [MERGE_GUIDE.md](../MERGE_GUIDE.md),é€™ä»½æ˜¯æœ¬æ¬¡åˆä½µçš„å…·é«”é©—æ”¶ã€‚

## é€™æ¬¡åˆä½µé€²ä¾†çš„æ±è¥¿

| é¡žåž‹ | æ•¸é‡ | ä½ç½® |
|---|---|---|
| å£å‘³æŠ«è–© Prefab | 3(Margherita / Pepperoni / CosmicPinkMarshmallow) | `Assets/Prefabs/PZ_Pizza_*` |
| ä¸Ÿå›žæŠ«è–© Prefab | 3(åŒä¸Šä¸‰å£å‘³) | `Assets/Prefabs/PZ_ThrowbackPizza_*` |
| é†¬æ±é«’æ±¡ Prefab | 17(ä¸‰å£å‘³ç³»åˆ—) | `Assets/Prefabs/PZ_SauceSplat_*` |
| æ–°æŠ«è–©æ¨¡åž‹ | pizza2ã€pizza3(å«è²¼åœ–) | `Assets/Art/Pizza/` |
| é†¬æ±æè³ª | 17 | `Assets/Art/Meterials/` |
| éšŠå‹çš„æ¸¬è©¦å ´æ™¯ | test.unity | `Assets/Scenes/` |

å·²åœ¨åˆä½µæ™‚ä¿®å¥½ã€ä¸ç”¨ä½ è™•ç†:GUID æ–·éˆ(ThrowbackPizza Ã—2ã€test.unity)ã€å£å‘³ enum å€¼å°èª¿ã€Hawaiian â†’ CosmicPinkMarshmallow æ”¹åã€‚

---

## ç¬¬ 1 æ­¥:è®“ Unity åŒ¯å…¥ + å…¨åŸŸæª¢æŸ¥

1. åˆ‡å›ž Unity è¦–çª—,ç­‰å³ä¸‹è§’åŒ¯å…¥é€²åº¦æ¢è·‘å®Œ(pizza2/3 è²¼åœ–è¼ƒå¤§,ç´„ 1~2 åˆ†é˜)
2. æ”¹å enum æœƒè§¸ç™¼é‡æ–°ç·¨è­¯,ç­‰ Console å®‰éœä¸‹ä¾†

- [ ] Console **æ²’æœ‰ç´…å­—**(é»ƒå­—è¨˜ä¸‹å…§å®¹,ä¸æ“‹é©—æ”¶)

## ç¬¬ 2 æ­¥:å£å‘³æŠ«è–© Prefab Ã—3

Project è¦–çª— `Assets/Prefabs/`,é€ä¸€é›™æ“Š `PZ_Pizza_Margherita`ã€`PZ_Pizza_Pepperoni`ã€`PZ_Pizza_CosmicPinkMarshmallow`:

- [ ] æ¨¡åž‹é¡¯ç¤ºæ­£å¸¸,**ä¸æ˜¯ç²‰ç´…è‰²**
- [ ] Inspector æœ‰å››å€‹å…ƒä»¶:Rigidbodyã€Colliderã€XR Grab Interactableã€Pizza Projectile,**æ²’æœ‰ "Missing (Mono Script)"**
- [ ] Rigidbody:Mass = 0.3ã€Collision Detection = **Continuous Dynamic**(ä¸æ˜¯å°±è¨˜ä¸‹ä¾†å›žå ±)
- [ ] XR Grab Interactable:Movement Type = **Velocity Tracking**ã€**Throw On Detach æœ‰å‹¾**
- [ ] Pizza Projectile çš„ Flavor æ¬„ä½:Margherita / Pepperoni / **Cosmic Pink Marshmallow** å„è‡ªæ­£ç¢º
- [ ] ä¸‰å€‹å¤–è§€è‚‰çœ¼å¯å€åˆ†

> çµæ§‹èªªæ˜Ž:ä¸‰å€‹å£å‘³å„æ˜¯**è‡ªå·±æ¨¡åž‹(pizza1/2/3.fbx)çš„ Prefab Variant**â€”â€”å£å‘³ç”¨ä¸åŒ 3D æ¨¡åž‹å‘ˆç¾,è€Œéž PREFABS.md åŽŸè¦åŠƒçš„ã€Œå…±ç”¨ PZ_Pizza_Base åªæ›è²¼åœ–ã€ã€‚æ¨¡åž‹ä¸åŒå°±ç„¡æ³•å…±ç”¨åŸºåº•,æ­¤çµæ§‹åˆç†;ä»£åƒ¹æ˜¯ä¹‹å¾Œèª¿ç‰©ç†æ‰‹æ„Ÿ(Massã€Throw Velocity Scale ç­‰)è¦**ä¸‰å€‹éƒ½æ”¹**ã€‚

## ç¬¬ 3 æ­¥:ä¸Ÿå›žæŠ«è–© Prefab Ã—3

- [ ] `PZ_ThrowbackPizza_Pepperoni`:æœ‰ Rigidbody + Collider + **Throwback Projectile**,ç„¡ Missing Script
- [ ] `PZ_ThrowbackPizza_CosmicPinkMarshmallow`:åŒä¸Š
- [ ] `PZ_ThrowbackPizza_Margherita`:**éšŠå‹æ¼æŽ›è…³æœ¬,ä½ ä¾†è£œ**â€”â€”é–‹ Prefab â†’ Add Component â†’ `ThrowbackProjectile` â†’ å­˜æª”(Ctrl+S)
- [ ] ä¸‰å€‹éƒ½**æ²’æœ‰** XR Grab Interactable å’Œ PizzaProjectile(æœ‰å°±ç§»é™¤:ä¸Ÿå›žæŠ«è–©ä¸èƒ½è¢«çŽ©å®¶æŠ“)
- [ ] Collider çš„ Is Trigger **æ²’å‹¾**

## ç¬¬ 4 æ­¥:é†¬æ± Prefab æŠ½æŸ¥

17 å€‹ä¸ç”¨å…¨çœ‹,æ¯å€‹å£å‘³ç³»åˆ—æŠ½ 1~2 å€‹:

- [ ] é¡¯ç¤ºæ­£å¸¸ä¸ç²‰ç´…,å½¢ç‹€æœ‰è®ŠåŒ–
- [ ] åªæœ‰ MeshFilter + MeshRenderer(æª”æ¡ˆå±¤é¢å·²ç¢ºèªç„¡ Collider,è‚‰çœ¼å†æŽƒä¸€çœ¼å³å¯)

## ç¬¬ 5 æ­¥:è‡ªå·±çš„æ±è¥¿æ²’è¢«å‹•åˆ°

- [ ] é–‹ `Assets/Scenes/SampleScene.unity`:Systems(å››æ”¯è…³æœ¬)ã€XR Origin(é›™æ‰‹ Samplerã€HeadHitbox)éƒ½å®Œå¥½
- [ ] æŒ‰ Play(å¯ä»¥ä¸æˆ´é ­ç›”):Console ç„¡ç´…å­—
- [ ] (å»ºè­°)æˆ´é ­ç›” Play ä¸€æ¬¡:æŠŠ `PZ_Pizza_Margherita` æ‹–é€²å ´æ™¯,èƒ½æŠ“èƒ½ä¸Ÿã€ç ¸ç‰†ä¸ç©¿ç‰† â†’ æ¸¬å®ŒåˆªæŽ‰

## ç¬¬ 6 æ­¥:å…¨éŽ â†’ ä¸Šå‚³

å›žå ± Claude åŸ·è¡Œ push,æˆ–è‡ªå·±è·‘:

```bash
git push origin main
```

ç„¶å¾Œé€šçŸ¥éšŠå‹:main å·²åŒ…å«ä»–çš„è³‡ç”¢ + GUID ä¿®å¾©,è«‹ä»–æŠŠè‡ªå·±çš„ fork/åˆ†æ”¯åŒæ­¥åˆ°æœ€æ–°çš„ main å†ç¹¼çºŒåš(ä¸ç„¶ä»–ä¸‹æ¬¡äº¤ä»˜åˆæ˜¯å¾žèˆŠåŸºåº•åˆ†å‡ºä¾†)ã€‚

---

## é©—æ”¶å¾Œçš„ä¸‹ä¸€æ­¥(æŽ¥å›ž BUILD_STEPS)

æŠ«è–©(Â§5)ã€ä¸Ÿå›žæŠ«è–©(Â§6)ã€é†¬æ±(Â§7)é€™ä¸‰ç¯€ç­‰æ–¼å®Œæˆäº†,ç¹¼çºŒ:

1. [BUILD_STEPS.md](../BUILD_STEPS.md) Â§7 æ”¶å°¾:æŠŠé†¬æ± Prefab æ‹–é€² Systems â†’ DirtManager çš„ **Splat Prefabs** é™£åˆ—(17 å€‹å…¨æ‹–æˆ–å…ˆæŒ‘ 6 å€‹)
2. Â§8 å®¢äºº Prefab â†’ Â§9 å‡ºé¤å°(Spawner çš„ Pizza Prefab æ‹–éšŠå‹çš„ä¸‰å€‹å£å‘³)â†’ Â§10~13
