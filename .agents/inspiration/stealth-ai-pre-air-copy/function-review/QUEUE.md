# CNC-96A one-function review queue

Queue order is dependency-first by syntax-derived internal call rank. Retreat jobs are closed and listed last.

## First ready

- CNC96A-FN-10862FD4BB80 — public static BotStationaryWatchdogExemption StationaryWatchdogExemption(bool weaponDischargedThisTick, bool activeRepair) — .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-10862FD4BB80.md
- CNC96A-FN-0768E72D45A9 — public static int ObservedRepairAmount(int previousHealth, int currentHealth) — .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-0768E72D45A9.md
- CNC96A-FN-A4C4B07E8256 — public static int FiringExemptionTicks(bool burstContinues, int nextFireDelay) — .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-A4C4B07E8256.md

## Eligible queue

### Dependency rank 0

- CNC96A-FN-10862FD4BB80 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-10862FD4BB80.md sha256 02bdce9a7d2543306539a9a0eb0c6d04930d77653f1e6d3f4b3614c3f38a1b95
- CNC96A-FN-0768E72D45A9 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-0768E72D45A9.md sha256 2e65c45c5441a96c47d6a6bbc7e11a75fd563ff24b7369eddec0b500707ae816
- CNC96A-FN-A4C4B07E8256 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-A4C4B07E8256.md sha256 2dcf84740ee3e44e1760dd8bfaa018f1f57033f881742472b675320bc8be28b4
- CNC96A-FN-9BAA6E457A56 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-9BAA6E457A56.md sha256 d36f782b2227096629e775fd002dba6d81f2ef1f0383064b1db08122c776d8a2
- CNC96A-FN-CF92E57D7114 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-CF92E57D7114.md sha256 c19674ef01337ef8582a2b23deea561c121bd01916f7a941a341b1483df6a006
- CNC96A-FN-FF68C9B44FAA test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-FF68C9B44FAA.md sha256 96920d70cd229d7ac466e82fd8230ea7eabd43defeb6f74dc7058cede0a4e00f
- CNC96A-FN-8167FCCCA106 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-8167FCCCA106.md sha256 657f45abf25bc9694cb3acafae3dc76ef4ea23c0e54987744da6770a17d29d05
- CNC96A-FN-B80A72DB1FA9 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-B80A72DB1FA9.md sha256 201418aa16f2b5396380c71b871eb8f94feaf4e4d9b4ebf4c9c86d0d45d4f776
- CNC96A-FN-FA07A0BC30BF constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-FA07A0BC30BF.md sha256 a540d3ecd0b7ccf3e734199ec5a35f6d7d31bbfad944e5e13e152dc77c3fbeb7
- CNC96A-FN-21C9A95B5D13 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-21C9A95B5D13.md sha256 034b1b55700c72add96cd4eb6ea50b049fdb88fea041b88de3f7e2e5f222ea49
- CNC96A-FN-4C68E5818351 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-4C68E5818351.md sha256 71880b89b96ff3b718fefc0700e76d646a99491d3634b520560fdee2010adadb
- CNC96A-FN-64C6E112BC53 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-64C6E112BC53.md sha256 d138004687f76d4985a623922a63a8b5d0617e59f97c99d7ccb7037734721d6e
- CNC96A-FN-D34144A151D6 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-D34144A151D6.md sha256 3e1b55874a2af6aab2fd5eba0fddfe222c8af4cdc9d10eda2944d5c9f17620d4
- CNC96A-FN-E06508589937 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E06508589937.md sha256 c276b962ec9b20cfd0e0f9353f2ec406329591935668be5db071dc86b9a7f73c
- CNC96A-FN-8587B2653EE0 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-8587B2653EE0.md sha256 440c7b701cfc20ba1064f5e030b4ff50e52e2a1f9ed765bb39262933284f0830
- CNC96A-FN-EDBBEE6A60AF substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-EDBBEE6A60AF.md sha256 76b790019e817d9bef4330ec1bf0b8c99bc089da5982d46834f58c5fa0f63dff
- CNC96A-FN-C5A906E46C6D substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-C5A906E46C6D.md sha256 d099a0e3d69064710645e0aba7b844803552cc4ae317c87da6f98cd9a3f04e84
- CNC96A-FN-ACFEEA330CB3 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-ACFEEA330CB3.md sha256 f8b2bb1c1d67a3a3376ad7e0a65574acffbbc9126e13e76e23a75e153576d918
- CNC96A-FN-452F3918CC13 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-452F3918CC13.md sha256 de9d0a99a5e44f6ff33aec7ec446189e907c702140906e9c6f1ab289298c6ea3
- CNC96A-FN-6231A086AAFB substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-6231A086AAFB.md sha256 918299983e354e854f1e75db319d2c4855fddbab77e84949842c9ffb42a2cabe
- CNC96A-FN-C1DEDF484247 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-C1DEDF484247.md sha256 4b8d30abf03e4145a5b5c47bfe45ec6d519b923e7089a0870db1e6b22d0dc5a5
- CNC96A-FN-86DC10E1F57B substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-86DC10E1F57B.md sha256 d9535880d3f95f777e0fac2e744550f3a469ede695e77e159fb53f1a74597d7b
- CNC96A-FN-8CF0395F4F4A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-8CF0395F4F4A.md sha256 f5d4ecd6816768bd799e8fce5eee8fde7ba6e63d35b07440e432de8655f999ac
- CNC96A-FN-735BB1952F56 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-735BB1952F56.md sha256 19643f003f2a433cf499a2953328d10905c4b64348e1a89b9b5139cffa4eab5c
- CNC96A-FN-B036325AA7A8 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-B036325AA7A8.md sha256 3d36117dd8c1cdfad67ef7e87188ca0ad69cc57fd503c0056ecc9e665780cafc
- CNC96A-FN-46B5C83A8729 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-46B5C83A8729.md sha256 4f48baabe910b693e67011f9c4f55a63f0864375c244b13ed930fc96c05499ad
- CNC96A-FN-956A6252395D substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-956A6252395D.md sha256 50bcca0ac787caa56155eb03603047c1883ab9d03a1d7d5cf315ad0f6ced036f
- CNC96A-FN-5020E8688F0A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-5020E8688F0A.md sha256 2b725560bf3e9d6d9beac8a9738be85e82974da116aca1ee13a54720cdbf76b5
- CNC96A-FN-EEC02BC271EB substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-EEC02BC271EB.md sha256 457f19cb98ed36dbecdae04f85b6590c200fd059138bb76eddcc3e3edf2175bc
- CNC96A-FN-1B453DEC45A2 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-1B453DEC45A2.md sha256 bfa973ca9a71832093bfb77ad7f3f511653530b31844e476afb4eb24b5f8f5d0
- CNC96A-FN-600F4693E19D substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-600F4693E19D.md sha256 d8734f0f3fee91d580c5f3676ae8c4792c31497f6d67ded62eb1bd1800e776e5
- CNC96A-FN-AB4A32EB4AA9 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-AB4A32EB4AA9.md sha256 db25388ce2a440e001f69b877f0e436cff8771ae184bc15082ca2ac6d6c8b151
- CNC96A-FN-D030F65C4A79 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-D030F65C4A79.md sha256 748de7412bdb77b3641484289f3e95112b77f9514b630063510b55700cec67aa
- CNC96A-FN-D785F92F6BEE substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-D785F92F6BEE.md sha256 9e458612bc148cac1acc07c3893229d12c486f97cc6f3dcabceeebba9c3796b5
- CNC96A-FN-262803A34A38 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-262803A34A38.md sha256 3f2e6b10fcfae74af472337ff944e94b0519975faa353a5af836bb758b642aa9
- CNC96A-FN-B2F00C5572CC substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-B2F00C5572CC.md sha256 20fc002a54691bca60316f1393f435aea8e3d601f2dc11ac4eccea443dfa8a06
- CNC96A-FN-30CDF3188FC4 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-30CDF3188FC4.md sha256 33e896a636fcd56754aed399380a1e24f484ba74c4a2251fbabce840b8c4d378
- CNC96A-FN-DAA6127D3A32 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-DAA6127D3A32.md sha256 05368da66f61aea1a2796f0aad93b7e266363ec4e8b33d814847acd3e212289e
- CNC96A-FN-03BDDBEB2140 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-03BDDBEB2140.md sha256 34fa45de2d7823ca12a2e61c765d8f0ca23ec5d754809aa6a952c9550d2f1754
- CNC96A-FN-5B79C698C9E6 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-5B79C698C9E6.md sha256 4b456ca6d84e1740f0ea60568f3e6056f43ac8a9c3db618a4f374a3b70c4241c
- CNC96A-FN-693AE3CC121A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-693AE3CC121A.md sha256 cd356cdf09117e9e299dd5ca966e768e709d0182fca97c76364f0d60bbab824d
- CNC96A-FN-FFD130CC4D88 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-FFD130CC4D88.md sha256 b654a6c148fabc8a1976e01fe40141ae8795ed930f811a680dbf0d6c361f2fc8
- CNC96A-FN-0362E161B3C2 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-0362E161B3C2.md sha256 2e5eed64021282946a63b969d17ff77e5403a94f191ce6613c0dbc7e7eda638d
- CNC96A-FN-80166B449CD5 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-80166B449CD5.md sha256 732ed698eebfae8c9df69323e8ba4d6fe323f922b0bd0770c8669fb51920c4cc
- CNC96A-FN-1E655A319999 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-1E655A319999.md sha256 30d03159c31d0a853cdf5ecefb9f0350f6d7ffbf7fd62380977c2840978debde
- CNC96A-FN-769F757A3C2C substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-769F757A3C2C.md sha256 316e9d482eff1f1b65de8db6bb0669d7075c1f7316c001970d63c8931cfbf12c
- CNC96A-FN-6EF96899F7C7 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-6EF96899F7C7.md sha256 856f6ed8b410f45ac8314437d6a4e42eea093020554eb3b893c4186d13e2049d
- CNC96A-FN-F21490521A4B substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-F21490521A4B.md sha256 387c298c2757c913b975813f090fef3e7f44c3a0c1a5c6530019dec0939b99c2
- CNC96A-FN-262BF2229BEC substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-262BF2229BEC.md sha256 bba9158d081485103c8f132a4faf75d23e426aaae83b5821590093ab90783683
- CNC96A-FN-98208303E890 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-98208303E890.md sha256 04ba5d77722b10b46961f97953f0e66122c9f3f93d7a626c565ac7ba8fcb46e7
- CNC96A-FN-3F3B0D7D81C8 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3F3B0D7D81C8.md sha256 29d6e83ac2aa238659b5b9ca8f2616895d3a874be3c15835eca4db9b17bb931e
- CNC96A-FN-8FE742AE85D9 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-8FE742AE85D9.md sha256 69d63a05d3b2fcd636dea73a43dfb17c2944dea48911b522e2234adf54ea12d8
- CNC96A-FN-587A04C0F70C substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-587A04C0F70C.md sha256 0f8036250d1f81c79e505b6574b1578602284aabdf6a2ff2ff33dadefcc9b772
- CNC96A-FN-F298BD716C54 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-F298BD716C54.md sha256 6621cd7e12a72b6f64e8397da09194b5590b48b53916079e841aad5952ddba5a
- CNC96A-FN-DED9CDA2F63B substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-DED9CDA2F63B.md sha256 1ea86630adbc6bd0ed06d81558cfbf81ac71eab492c1d46014aa1cdbb0d808ee
- CNC96A-FN-11E6F17DBDE4 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-11E6F17DBDE4.md sha256 e29af52c78a911f719e2bb28211dd321dc8fb1cbe9cc0551509a24c47c9fdfa5
- CNC96A-FN-926B1857FCAB substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-926B1857FCAB.md sha256 1c17a8cd78b369972d92bbe524f4ed9dffe5905baec4df2d48e4bb13627a061a
- CNC96A-FN-6E2169D9F443 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-6E2169D9F443.md sha256 0d3108a746e78963c2d685cf081b3018f469ca7b700da130d4bbada5e3a8fc64
- CNC96A-FN-21A541E3ACE9 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-21A541E3ACE9.md sha256 90237d345050e005e47a686cfbc23f21764b67acb1f810f6aef81430d395cb3a
- CNC96A-FN-90E25B8D7663 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-90E25B8D7663.md sha256 1d561b5ba6198a44fe4c0fc062293a0fbbb4f42837dbcc0bd3c8d987d2330159
- CNC96A-FN-2B8A1C8B25A2 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-2B8A1C8B25A2.md sha256 4014dbeac0200fc949b0949a005eaea32995c5f9e6613fcf1e07c67a1a365184
- CNC96A-FN-C7D9CB4F388A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-C7D9CB4F388A.md sha256 4ae0c6e0e4a19f199043b19a462fdc8acbc92313f16700bbe25f0d19611fb640
- CNC96A-FN-C1650AF19AE5 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-C1650AF19AE5.md sha256 898eb6976aa5c233fc72e693341024a768059b1d2e66b70ac3346c3303287679
- CNC96A-FN-9AAF88E48173 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-9AAF88E48173.md sha256 0cf3c440e96383220b79ac01714f293b42649ef4f98d8c228452d7be6245d9bf
- CNC96A-FN-ECFEEB8BBDD9 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-ECFEEB8BBDD9.md sha256 eb006f129bb6e3395838e17133202ca8d602ea2ed51db5bfb706ef4f865fa902
- CNC96A-FN-4EE38893644D constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-4EE38893644D.md sha256 2e28e473a777649f62c60584d7210f87953d2cba798c9700d2567f2e8f0d686b
- CNC96A-FN-CC1934993DB8 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-CC1934993DB8.md sha256 242bdb0cc20dd53d0d603eb82c8e9f11bbe37666586cdcda5e737cf86c8aefbd
- CNC96A-FN-844C01F7CA3F constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-844C01F7CA3F.md sha256 99cee612397eaba4340a182e6fe198601a7f6f5b3d441a00f1a20db177697a78
- CNC96A-FN-FC863CCE421B constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-FC863CCE421B.md sha256 729a7994f4fb3641e01f41b0aa2ad958f442bbfecba7928c2f546ed683a403b2
- CNC96A-FN-6603510ED64C constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-6603510ED64C.md sha256 2cc768832133fecff6b5530d7851938d55e2779ff45927d32b3a1c7dcbabe4fd
- CNC96A-FN-D3003C07F306 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-D3003C07F306.md sha256 b943314682e464ceebf7da48d2db1644ced6c48dbf68f0a3e8cf1a994ace108f
- CNC96A-FN-E7786086FF67 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E7786086FF67.md sha256 39de47b66ed1cdc62110f08e9c7a65e9d0a7f970dff35bd0ffdba0969f4b0b6c
- CNC96A-FN-7899189DBD3D test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-7899189DBD3D.md sha256 3d14f582af632fb60366817c5f63d0e39afd3c4ab0cc2004b37ea4c4d1f35068
- CNC96A-FN-275A6B9C5AAE substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-275A6B9C5AAE.md sha256 80fcf4689651d2a4e09e7fd54960a7d9f9f8c421f13695045ff15722b5331088
- CNC96A-FN-1603A9B999EF substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-1603A9B999EF.md sha256 eb2e873876334cec4b81ead27faeb72056a1a7efd45428e4869bf9ebcaf0f440
- CNC96A-FN-E0E58762D260 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E0E58762D260.md sha256 765e358f2247aac25163f662d691cec6b8bfaf0fa4e1481a723cf118e4452d46
- CNC96A-FN-9E84B45DB825 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-9E84B45DB825.md sha256 dbbca451ffcd28a4e274283166c7aefa1eddf048a03fdc021a15f900b89ef824
- CNC96A-FN-08DEFB74BA32 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-08DEFB74BA32.md sha256 44e58cdaeb10482d52d69b2335efff2cd2d729adfdf8664d5751c1c5274afc60
- CNC96A-FN-6F037D735ACA substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-6F037D735ACA.md sha256 cafa8a021da3cb22baaf31b54dbefb416d8aa877d0d6738c37c9386251e0e47b
- CNC96A-FN-3DD07AA039AE substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3DD07AA039AE.md sha256 2a338f93a2813081a573b852c6b3a66c7976de2d28cc7149f55d4fcd33ca71ae
- CNC96A-FN-63DC8C643A2D substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-63DC8C643A2D.md sha256 4d2edc59e772b402d1d32673a4bbdc150f35936bd738268c58d6f3372fe0f46a
- CNC96A-FN-1C0B2713C45B substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-1C0B2713C45B.md sha256 aeffe7f69fb17877e4a17c55774ea1f22f1574cf4f39288f8398a928d53e0c27
- CNC96A-FN-9A8AA0DFB343 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-9A8AA0DFB343.md sha256 ffdd32b019d94494e8c4adfbe19da476e410313d74500b163c1ddfb52072abaa
- CNC96A-FN-8AAF95C08BEC substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-8AAF95C08BEC.md sha256 2f0ad2474d77d218e55b10e1fed8a0e7aa52c83a586ae72ae5517d20ef20ead4
- CNC96A-FN-05A303AB9A60 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-05A303AB9A60.md sha256 efb7ea5a2d141234fb2a06f03534c88abbd1b7552776552d55c9e0647d3e2490
- CNC96A-FN-37C1BE7F8151 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-37C1BE7F8151.md sha256 ab772484d02807fc4640d882f202245d90b326feea2f160dd11088396786fdf2
- CNC96A-FN-F8A2403C88EA substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-F8A2403C88EA.md sha256 0dc434a12d86e3849bdd1d9ced7c82c5ee0a8549899ecc5127301ef773eff6ca
- CNC96A-FN-E866B456DF2D substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E866B456DF2D.md sha256 22b25d7e9ca22f32efafeb288c2ea6c8cf0085476eb549a7f9c5b0626d02d2bd
- CNC96A-FN-59DB2912BCE5 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-59DB2912BCE5.md sha256 d032e61a490ef08c69b3263eb65f6da5502d11a6f132780b3082501efa06839c
- CNC96A-FN-0E77729B1DD4 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-0E77729B1DD4.md sha256 d101a583547343d612d51eeb87a9c6b5b95bffe268e8d9136da9697fa17575ca
- CNC96A-FN-A0D1B2722372 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-A0D1B2722372.md sha256 e4013705ca6ee29ddb5e533521d8df55f1a0ce259ea608389d688cd008befe0e
- CNC96A-FN-78896AE89AA2 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-78896AE89AA2.md sha256 922f4f100b6ae509bab06e605a835ea64191dd8ca9bfac73d77247f0ae195109
- CNC96A-FN-E3A118A3D1CE substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E3A118A3D1CE.md sha256 a776a8abf58ce7d5912d7ab600e14729406e8c45b391e08bce661c34b493895f
### Dependency rank 1

- CNC96A-FN-929DFA9E156E substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-929DFA9E156E.md sha256 11bc915a5db12e23eef8db770317057a8b2c28753d70950b1543227b63c2546a
- CNC96A-FN-F9F1EE8CB298 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-F9F1EE8CB298.md sha256 d22a5ce3a58d1b6a41172eb7bed2f01daa3aea3dedeede87299da83d507a8306
- CNC96A-FN-3FA54BA95A9A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3FA54BA95A9A.md sha256 ba3f361e0b522daf52115dddd6900418be6591185fdd545fdd35a2e5c14e2caa
- CNC96A-FN-4A18AAF662A5 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-4A18AAF662A5.md sha256 c143e02ffed3655366cc0d494673ff90e1ed58c6760e97a392d51b9c097adf8b
- CNC96A-FN-DE95637FB877 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-DE95637FB877.md sha256 9f4f37f95bd56e66a63a2845a73ccee492e5e2f6fae5a0e094b2c26a3c722d45
- CNC96A-FN-B351F3E03F1C constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-B351F3E03F1C.md sha256 25efcd28a2366e775ea169652d4c51ee43e3cf347ff542eb1402f2167ec9c225
- CNC96A-FN-E756C21C06C4 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E756C21C06C4.md sha256 f10954149a70da95165acf8a0c8c8fbfeb0e0b4ad02dd713af4ee37f8c51ac59
- CNC96A-FN-A0B176AD444A constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-A0B176AD444A.md sha256 5e3348444dc87a6c23ae1218ad92b609424ed640c67a2a171575ecbe7f2c73c1
- CNC96A-FN-D2672B2536B6 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-D2672B2536B6.md sha256 348b60149465ff1be18a838c272f2ccce61ecbbf8c90cc9178d8db59fe4c6da1
- CNC96A-FN-7B1117151FA7 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-7B1117151FA7.md sha256 0f33e6f416ad3f1ee98d68615e3250f9b229471ac6cade762a1f231a018a17a0
- CNC96A-FN-D02DC77FBE68 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-D02DC77FBE68.md sha256 ccd90713f4c24e3f924bc748e3536ccf43de7a02452ad2adaab1bdfa7f3bb5a6
- CNC96A-FN-ADD20465D7D7 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-ADD20465D7D7.md sha256 1abc90e937019426a23f454080a138b3be4cd5306d268d3cc1da99c859c00486
- CNC96A-FN-1675FD5BE194 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-1675FD5BE194.md sha256 b84dae95ff6d94fc6cecefef2717c51b2af1892f2e4f0d7a85e29023a7be2fdb
- CNC96A-FN-BDC91930911A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-BDC91930911A.md sha256 5b7e62e5f6fb7fe283b5935621141a87d5b60b8f8c6684c6241ec4b291fbd7b6
- CNC96A-FN-F5560DA311E3 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-F5560DA311E3.md sha256 ab1e55b675b61d01f98fca385cdaceb20a05d2955b17601ab90cad58c529e13c
- CNC96A-FN-5F94AED53553 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-5F94AED53553.md sha256 bf9177010a335667c79c6a9db5e31c93255586619aaafb0ff48321bbdd64a4e2
- CNC96A-FN-BD399E323A56 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-BD399E323A56.md sha256 f4b23f6bd4055eb2e3b49283bc69fa9703d0a0512b6b121002b1ed41e1fa4a23
- CNC96A-FN-4E99A6527BB3 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-4E99A6527BB3.md sha256 8417aa7c64858516338928399719fc7c7b4ac1ba8fa71ddc751957d3c92a59fb
- CNC96A-FN-7569FF5F3FB2 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-7569FF5F3FB2.md sha256 d9c2c7c6db4576f60eda26a85c545f96c4349740cc78367ef0941ab84a26814d
- CNC96A-FN-1F603BEE6EE0 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-1F603BEE6EE0.md sha256 5a88edaa6c4ac54c317ced95f6145e69ffff5a5d2b4ac485c38343f20faada8e
- CNC96A-FN-F440F05ED786 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-F440F05ED786.md sha256 881c3ec658389e96f31bebedb646b5e546ac2bd07a6d239a6ad5e432874af76c
- CNC96A-FN-3E1BF7F96158 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3E1BF7F96158.md sha256 8e438ff053e61a31aed8df6939e4517152dc0ba7bfff296e3f9536a70e340e8f
- CNC96A-FN-CA7F7D08AE66 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-CA7F7D08AE66.md sha256 e685595a476453c0f8abee4e7558332dddbd3dbe7c292f95d9e748512e449589
- CNC96A-FN-3F9F9A5DC8B3 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3F9F9A5DC8B3.md sha256 14295489c278279eecf2b2bbbc9c85c58f5f29cf6ce7225792c4e3a4dea485f5
- CNC96A-FN-6EDD50846A63 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-6EDD50846A63.md sha256 f96edee7e70600bc521d0c65876879df6d1476a323ed2a90f3dde6e4fa31931c
- CNC96A-FN-76EEBE996564 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-76EEBE996564.md sha256 997c1404f1debe5141c8d3b77f77486cfe210e6934c815c0fe2494c36f0de218
### Dependency rank 2

- CNC96A-FN-433FDED9C78C substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-433FDED9C78C.md sha256 b75cf4df79542eb2a801e6c695a0893915019531be2e0a7ff0a419aea0c7f75b
- CNC96A-FN-AE3F7FCE1BC6 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-AE3F7FCE1BC6.md sha256 c6125d3d223e8595285dec5b99de420fefdacf12cf79d4d544029a88f73d7d97
- CNC96A-FN-309094B02BB8 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-309094B02BB8.md sha256 8bc08e928bac792c19d31f7acff26d3b5bfe0f1afdafefeaad93bdfa454cfe45
- CNC96A-FN-DD807FC00018 constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-DD807FC00018.md sha256 cfc744090964f2a885f5f10c10a6f99faa70d89a00bdf840bdc327d06ade9c7f
- CNC96A-FN-2AC2ECF1433F constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-2AC2ECF1433F.md sha256 b8849fac9adf5a0508b9d86dc46bf4ce6ab972ef00f8dbc45b81c11a370b2bdc
- CNC96A-FN-BCA5B6D1D908 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-BCA5B6D1D908.md sha256 91a039302d86606eebd2a62a05093afefd631e811f68953416f3d583430380a9
- CNC96A-FN-E0358288BF0E constructor-config-plumbing job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E0358288BF0E.md sha256 fe2b1d0cd6e2609d9025a9c0c6a2ed571d248b9920792098f5144489f061b162
- CNC96A-FN-72BD390EEAB1 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-72BD390EEAB1.md sha256 47fdfebbf87a7607ce8a5acd6b2a27d07ccda55497465752a5ad9514eb31f3b0
### Dependency rank 3

- CNC96A-FN-E2E7201136E1 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E2E7201136E1.md sha256 04e81bcbfca04a3e987452599f79be02e627d6d2bd02eced74deb0acd3261be4
- CNC96A-FN-40F9D3DB6F67 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-40F9D3DB6F67.md sha256 3f03a7928ed56411899b7c1b20c1a08b030bfe878c7f05fe826c43e294dd7d4d
- CNC96A-FN-96C7EC3F9186 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-96C7EC3F9186.md sha256 5f63016f9f7f3d16026a12f8ffea57f05833626f68ae3dabc6de6aeaa4c13f6e
- CNC96A-FN-3D7D329646F9 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3D7D329646F9.md sha256 c58faa017e029c771fe52f998701b9c1b9c823895364aacf69485db2d209239e
- CNC96A-FN-64F509910F66 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-64F509910F66.md sha256 a4f0d31c6afaff2ee5935986301e69dce2e22d0326a0d6a86594e3a1175bb8bf
- CNC96A-FN-3C2AE0D876CC substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3C2AE0D876CC.md sha256 8c39ba554f1779b3c326f87c0c42eec435e3a7b91e9c0f76600a52bd82173be9
- CNC96A-FN-B70A464E87D3 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-B70A464E87D3.md sha256 6f54886bdc55039cc2cc5ceda34fe2a7a640591ff7397496e43244f48486de04
### Dependency rank 4

- CNC96A-FN-4C202343D77A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-4C202343D77A.md sha256 d9b3a7f3a86417a1a8bb44efc51476d64eb53a37d823cb70a05501600f62b1f0
- CNC96A-FN-760611A2A6C7 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-760611A2A6C7.md sha256 a1b049f8d94843f58b8feca0fcc83c989b33881ef165987f0b89d7c22b7b3601
- CNC96A-FN-2DA8BDC4CE2A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-2DA8BDC4CE2A.md sha256 7da76e11d0078c47483205fbe609f12f80ecf6c8619d14931bfdc58cb2f9dd28
- CNC96A-FN-4EC4985EF182 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-4EC4985EF182.md sha256 55bb28e26b92c734b987229a54c42559470dfca751c1e7b0429925165e1491a0
- CNC96A-FN-C64AEFE1687B substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-C64AEFE1687B.md sha256 9e3d0d143b0a66d95e96c482ed7c49b95538a56777d23b0840a85c517d185422
- CNC96A-FN-163554C8C89D substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-163554C8C89D.md sha256 c835f791dadc43bed90ad9d41142196585b60785f2da14fa3c6d36370ead0fa5
### Dependency rank 5

- CNC96A-FN-44B4F1A3CC3D substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-44B4F1A3CC3D.md sha256 c1e7e1185357dc71d30cdc33806a3c9c74dac0b8bf9f9772c38cff8f7f849bf9
- CNC96A-FN-ECB7E0F0C568 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-ECB7E0F0C568.md sha256 a4c8395b0655b5986bdfc3d7dd39dcda9c197c85c8002c512a8273b89a4cfd44
- CNC96A-FN-370F3833E227 substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-370F3833E227.md sha256 974ca1d894e8f2925e2c43d52500a1530cfce270ce713fca995297d78988977a
### Dependency rank 6

- CNC96A-FN-4099692E50BB substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-4099692E50BB.md sha256 09c49115893eb4ee67ba957a6eea32c1d56f1fa9a4627717031dfe4b3b3c49e8
- CNC96A-FN-420CCA8E009B substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-420CCA8E009B.md sha256 5ba685d99ab859cadc1c6c6d217e890dbe4e02aa8c7d32fa26c17952cbd0d348
### Dependency rank 7

- CNC96A-FN-039A14EF2BAE substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-039A14EF2BAE.md sha256 621c9d0cf03ff6b64da8420a745d08626f9774c2c630793fa56b167da94c31fb
### Dependency rank 8

- CNC96A-FN-83C409535BEE substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-83C409535BEE.md sha256 5c388de50ef6e30062c0b985f0011122dbb9fd475aaf0d2f63ca47731046075e
- CNC96A-FN-49D73096388A substantive job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-49D73096388A.md sha256 c78c165a14e86669901d38562674d1d451795c8f3fc04d74ebfcd7131b872cae
### Dependency rank 9

- CNC96A-FN-C2E1EDB3B931 test-only job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-C2E1EDB3B931.md sha256 77fbf2a4d88cbffce431b2dcb770b4a2f2651def5fe46614ae4f8ae56d748a33

## Closed retreat/completion-retreat queue

- CNC96A-FN-2BD08A9C9CDC LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-2BD08A9C9CDC.md sha256 9e200963161a5c4433b47bade6aa24a3127b23a967bb78c611ce8c7166a8ee2c
- CNC96A-FN-B0D9EE091849 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-B0D9EE091849.md sha256 0ffe7f4f201c25c967386fa234cea1bc41c7eba8bd791fcc183d8db9da60857c
- CNC96A-FN-13BE4F1DD93E LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-13BE4F1DD93E.md sha256 6f299381c79516f7f5c984eac4d697659b817b4a01130a28c83f3f6aef744a88
- CNC96A-FN-B5CF29341BD4 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-B5CF29341BD4.md sha256 782e44d0a2ddb652937800a27b2907ee26f9ecafb556410a16cfe79150db1b4f
- CNC96A-FN-B023E2D1111D LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-B023E2D1111D.md sha256 d938d2598f256bf0df9a5244611bb433b49afad44a1e8797eed781ea7bcda1d9
- CNC96A-FN-9B7FEC1576AA LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-9B7FEC1576AA.md sha256 adbd188c72aca3d608fba00e534c8d7fa72c7ce6642a8a98b344d798f41480f7
- CNC96A-FN-CE2E6C4176F6 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-CE2E6C4176F6.md sha256 e72832834ba4cadf854e1917b4f2a3615129f925df72012fc5a59edb93315498
- CNC96A-FN-E3744DF379F3 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-E3744DF379F3.md sha256 1d0ad1cadf52a2ad3fb8be320561092e0bcf3948180fd972499883056aba6de7
- CNC96A-FN-1FC15E173406 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-1FC15E173406.md sha256 7091e6472304c85fcca920e05afa7942e4db10fa4e6c87c365a1720aa8b6c282
- CNC96A-FN-9A9DFF3DF271 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-9A9DFF3DF271.md sha256 c354edaf837d1064aa3fa9d000fb6f1614d4954e2ea451898ce52773efb75687
- CNC96A-FN-CF517A6289A9 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-CF517A6289A9.md sha256 e9d3a273e9998a4eff2d46751b951882d66c58083848da0fc0f06a6902f3553e
- CNC96A-FN-CA67978FAA43 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-CA67978FAA43.md sha256 caf25be1a624c7c3defe16a0216d27707e425243995332b0077637998c73598d
- CNC96A-FN-5A882A56AE37 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-5A882A56AE37.md sha256 a5910a15306d8c3c0a3e2721899e0822615afcf0ea71bd010177b1c459c4040b
- CNC96A-FN-C5DFD755A127 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-C5DFD755A127.md sha256 4e229e91c05789f1288d917b77c0c11d0fa50bf4110a658e2ee560470377b531
- CNC96A-FN-32CC02D5E637 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-32CC02D5E637.md sha256 466981d5e20868d6c14e20d8d5df0ec96686395d3afef22ff76186c8bdde51ef
- CNC96A-FN-74C20971730D LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-74C20971730D.md sha256 41c7e5705614b2f748fa009f2003891396628ac20fd39845f7cb4902cfeea0a3
- CNC96A-FN-8438307BB011 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-8438307BB011.md sha256 915d9ac642984933edf41c470ee79e92b5a397eea4d7302ff4fc5515e4800c5f
- CNC96A-FN-AAE1F204C053 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-AAE1F204C053.md sha256 7b2620cf70ea776dedec64f47aaf78ccdff12ae88b56b6920d2765f741da275c
- CNC96A-FN-86161F4A8F8C LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-86161F4A8F8C.md sha256 64dee663d93e09035a95cab5813aa690c3f243496b17662f8a6429d59b2d7d99
- CNC96A-FN-1D2737BDB051 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-1D2737BDB051.md sha256 f090a4541be0a48b810037de5810e4e0d9ab759f717c85f7bbe0f7c3006ced68
- CNC96A-FN-9F61A34BEC41 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-9F61A34BEC41.md sha256 e5bdc69bdebf77f1db6b2bf620abf73ea8ce58e6bb6350fd3f7d403704d07897
- CNC96A-FN-9ECFE92700F5 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-9ECFE92700F5.md sha256 0321bd6c07b5113cc384465ab54f9129ff70ccb4f6e1ef4edf5b6e7725eab777
- CNC96A-FN-010528118B19 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-010528118B19.md sha256 61246db5b1c896f9b8f48509c9e97629d7c6fd1e23ceb45ede63321517c6edd3
- CNC96A-FN-AFBA467E47C0 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-AFBA467E47C0.md sha256 7718f9941a2bc0e4904a063de34330dd707a8be7317d2c28b2905a920225bf79
- CNC96A-FN-3924DA85CC50 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3924DA85CC50.md sha256 dbdda6e2c8be13fe1edd894b99911316a5c747e2ba841cea1131f638b1be18b8
- CNC96A-FN-00AE120A8253 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-00AE120A8253.md sha256 951eaa116c86ed0a4a8d3a02b1c6eeb63611e664795bfa28caca150e732a4f4b
- CNC96A-FN-0BD115A6AB20 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-0BD115A6AB20.md sha256 499a53799d943d679a58ce7206bee02e77bed65c405d1db459a512d03a7f7237
- CNC96A-FN-70880D45F7CD LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-70880D45F7CD.md sha256 f00005fe4b9f2de8b1fa6c260ef623b76936231a8e8a409ff9703095145a8db1
- CNC96A-FN-044402DA7972 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-044402DA7972.md sha256 1bf6e6f95f172ecd04f1e25c7a2ef5e70b29e1b9cb7e36a0a54554352a9f235c
- CNC96A-FN-77894A78AD0E LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-77894A78AD0E.md sha256 cbda828ee24e4d242f03a6a27fc0115868b9a34d035ed859a3517ca4595087c1
- CNC96A-FN-3F1B4A42109C LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-3F1B4A42109C.md sha256 10cd4a3882c46ecc931016e8ff3e50d15e392dee0adc5a28c6b60487ea85dde6
- CNC96A-FN-530C8FAC9240 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-530C8FAC9240.md sha256 a62caa07209b335396c026d133b113cbef1dbda9d078b92bcdff28acae35a224
- CNC96A-FN-605DE014E540 LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-605DE014E540.md sha256 ac895328899b27fac9319e6cad2b5f2f4e6a1d6b295b13763c51f5671ae578c1
- CNC96A-FN-FE50F0A3924E LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-FE50F0A3924E.md sha256 e30dd0b4c81c9ee0910f4bb753c6808ee4781c65e3d43ae3d0dc0023f8545999
- CNC96A-FN-94FD4382367D LEAVE_OUT_RETREAT job .build/cnc96a-air-copy-function-review/jobs/CNC96A-FN-94FD4382367D.md sha256 4b722f37e9736b829b232b7b70b6834046dbf8baf42547003665b92875e7b36c
