# World clock data and skyline attribution

City coordinates, population, and IANA time zones are derived from GeoNames `cities500`,
licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).

The seasonal skyline images are TrackMeUp-directed Urban Wash watercolor artwork.
Their exact generation, intermediate WebP, and packaged PNG manifests are stored in
[`SOURCE-MANIFEST.json`](SOURCE-MANIFEST.json),
[`RUNTIME-ASSET-MANIFEST.json`](RUNTIME-ASSET-MANIFEST.json),
[`PACKAGED-ASSET-MANIFEST.json`](PACKAGED-ASSET-MANIFEST.json), and
[`PROVENANCE.md`](PROVENANCE.md).
The repository and packaged asset locations are summarized in [`ASSET-MAP.md`](ASSET-MAP.md).
They are not included in the repository's MIT grant; see the repository's
[`ASSET_LICENSING.md`](../../../ASSET_LICENSING.md).

Release status: Owner-authorized for public publication on 2026-08-30; applicable ImageGen service terms accepted.

Intermediate runtime transformation: Scaled and center-cropped to 1280x720 alpha WebP with FFmpeg/libwebp quality 82, compression level 4.
Packaged transformation: Decoded the reviewed 1280x720 alpha WebP runtime derivative and encoded a lossless 1280x720 RGBA PNG with FFmpeg/png, compression level 9, mixed prediction.

| City | Season | Asset | SHA-256 |
|---|---|---|---|
| Abu Dhabi | summer | `Skylines/abu-dhabi-summer.png` | `9f6549fb6a1e861ea721c82eeb24738baa9c1f6f1973276c9a4db0852ce92641` |
| Abu Dhabi | winter | `Skylines/abu-dhabi-winter.png` | `3d4aba08b45e4db6603e747632f08f43dc329d5b6f5a463820e7e9f6e7e490ea` |
| Abuja | summer | `Skylines/abuja-summer.png` | `2b83c1b0e43d450b194151785f59a3dc6c0f70766fca30b739811fcb7b47e06c` |
| Abuja | winter | `Skylines/abuja-winter.png` | `fa2bfb3da02652d02da297e5f81d63fb709d52e6425369416fc58ecb7c43b39a` |
| Accra | summer | `Skylines/accra-summer.png` | `50f4f2fa9baf7c9f198e01142e1b180ab0fe88db0b5abb9d7352de74ca5bc876` |
| Accra | winter | `Skylines/accra-winter.png` | `bbb85d3541215e576ee3acad8cd89ff65beec20775ee2d1f62b61c4773d4be1c` |
| Addis Ababa | summer | `Skylines/addis-ababa-summer.png` | `85804bd981d23ee322a2dd37edc38731f92985f456cfe0fcfff154a1b7714a26` |
| Addis Ababa | winter | `Skylines/addis-ababa-winter.png` | `fc6833883976a50c4349a74f4b61882f4fc722e1f3368a7d038b443051c7db11` |
| Adelaide | summer | `Skylines/adelaide-summer.png` | `2cdfc13eb2a1c0ed7a3755f184d7d719884cc3bc309b3e4917a433e99b33c27e` |
| Adelaide | winter | `Skylines/adelaide-winter.png` | `f2d3c5fc859c2d162bf5a187973def7c9da5f3aa1f0449be6f1b2726c4f2054b` |
| Algiers | summer | `Skylines/algiers-summer.png` | `292dd63ddca39fdcb9a1004f63f7a56fe1385aa9dbd0d5c221c85b2a9553ab3c` |
| Algiers | winter | `Skylines/algiers-winter.png` | `394c7e37b8cb9d359f35831ecdfb67baa2dfd8762a0bf68923d19a7a9d62172c` |
| Amman | summer | `Skylines/amman-summer.png` | `e7cdf93377eba59c576975f4f1868cd8a8efe6eb52bd417b90cf402f6dd718d2` |
| Amman | winter | `Skylines/amman-winter.png` | `e4ca3ec087e61ff0d4af197177feeb9019d7ecd4230ead6945f15fc1a66b9f5e` |
| Amsterdam | summer | `Skylines/amsterdam-summer.png` | `89e78acd3ce1852f434b061a4e7b5f4b7d6c1f3e9b24ce28c9a3008590633839` |
| Amsterdam | winter | `Skylines/amsterdam-winter.png` | `6ddf09907abf239844e0b79ab0c9ef2b0c2d2f4db3681a9f9f943d58116e1d75` |
| Andorra la Vella | summer | `Skylines/andorra-la-vella-summer.png` | `ffb250a241a9b2bcbf18353c019552e2675077c98de52b4b7b7171cd8419f222` |
| Andorra la Vella | winter | `Skylines/andorra-la-vella-winter.png` | `4f514a3517fea1d16a7044fd6856928a43ede36e920542b3aa20d88f998f6d4a` |
| Ankara | summer | `Skylines/ankara-summer.png` | `e343d10e5b34f93f2e6111ec09b797d965caef90a4ddd08468b46d24a411c1e0` |
| Ankara | winter | `Skylines/ankara-winter.png` | `edfb7e643b7a76521c8fabb0a8823606af24eed9837bb6aa709a6ff813e0afce` |
| Antananarivo | summer | `Skylines/antananarivo-summer.png` | `85eb64ccbb027deca7f543f8f969e65d4796adedb856c52d6e3cc797916ad764` |
| Antananarivo | winter | `Skylines/antananarivo-winter.png` | `ff832e823756e8cde9763edae37f914ebf2a54f9862ddbad0d77f9ee1d7dd15d` |
| Arequipa | summer | `Skylines/arequipa-summer.png` | `2869548502f8391ee2a771655060a96d6a99c836bef6edbd65a919aebca9203f` |
| Arequipa | winter | `Skylines/arequipa-winter.png` | `01ef4a4b41fa5dd23875200e5412eb8061190c23ae3c6580ec22ca3bec3d7b84` |
| Ashgabat | summer | `Skylines/ashgabat-summer.png` | `e149d28446cc516b781b1f9050cab41b3c8d85b4b80d7f5fdf3be07102e40707` |
| Ashgabat | winter | `Skylines/ashgabat-winter.png` | `3d63e07c5b698ff7304d8912fb442a994ad0e8abdc680fe2d40f8b8f70bf4c6e` |
| Astana | summer | `Skylines/astana-summer.png` | `eb03ec940f1fac0f8d39bc9411e65a00bfbe4af13c0e27a51887d0c66822dbe9` |
| Astana | winter | `Skylines/astana-winter.png` | `7fe606ec2c45bd82aba26c4aede390082c8326b245fd9a22804cb0244e8ca2a4` |
| Asunción | summer | `Skylines/asuncion-summer.png` | `d22eff363f56506b60d9517e06b9415a519e9b0803daf8568f005004cd9243df` |
| Asunción | winter | `Skylines/asuncion-winter.png` | `dc8a13e937edb5c4cc20fc27933d370a5f5fa2f318a7d0e3934621fb4095cf11` |
| Athens | summer | `Skylines/athens-summer.png` | `0d24ec679e9c10e2f1355658d35b42b4d9eeaf4805c70631337007896c86da7c` |
| Athens | winter | `Skylines/athens-winter.png` | `4f951ec0bd7212388207e29147e1a63f0fc8c8d8ebf3763a8312548c82392ad8` |
| Baghdad | summer | `Skylines/baghdad-summer.png` | `22da48877adb4816507bf08d87bfde748a8c67d2d488d71191beafb22a1800c4` |
| Baghdad | winter | `Skylines/baghdad-winter.png` | `076133521980f7c34c045dda3413349a9d746723f2451fb064c7073ffd070e69` |
| Baku | summer | `Skylines/baku-summer.png` | `784f02f7239ebf9800879a0a36ec7990af0c39ed3f6936ce3b1f1472bfc569ef` |
| Baku | winter | `Skylines/baku-winter.png` | `ce437f4665a87e6678183d0c70d26526ee06259a3ec8beccd66ee9c3ad96a412` |
| Bamako | summer | `Skylines/bamako-summer.png` | `7e437a9c46b90e59d4fa411a2799014ad4528594e2a4866034702debccb4e3d3` |
| Bamako | winter | `Skylines/bamako-winter.png` | `1221c59c3632484154b83351227bdbc23aa34762b37a976952c202bb55cc645a` |
| Bangkok | summer | `Skylines/bangkok-summer.png` | `a14487398a8309065a0d843259be87fbbcf441404f9b8d46123ec5d7eac356cf` |
| Bangkok | winter | `Skylines/bangkok-winter.png` | `116352cb20039835fff99d2d9a4c9a6b19e4cec32db8b7b1a0cb18d004cd7c78` |
| Barcelona | summer | `Skylines/barcelona-summer.png` | `5f2bac57e8d7c4a5ed6770e77a1c716773709c7cdc25ed349a85b225b879f9d3` |
| Barcelona | winter | `Skylines/barcelona-winter.png` | `4651f6ec2eb5ad602933e6c3440f0fc88cdf64086c8fb16a95e4556dc53528da` |
| Beijing | summer | `Skylines/beijing-summer.png` | `9bd9736c5a76c69eae78634702e10b3328468f4fb4ccc03b7dde0e6d3ecba2e2` |
| Beijing | winter | `Skylines/beijing-winter.png` | `3d36f2ecbe1ed7cdd7f27c89d8a9a6addf99a4bc8bc2246abd68184148ae854d` |
| Beirut | summer | `Skylines/beirut-summer.png` | `9b9358e8ded19c9a4d36c9dc5e6e206842122614184dac608d14c82eff553eef` |
| Beirut | winter | `Skylines/beirut-winter.png` | `8f44ee7e0f9b9d01c8d8b77e534ebcd886090d40214f998630ea0b21239e1919` |
| Belgrade | summer | `Skylines/belgrade-summer.png` | `6e349f1c30b5f8517c6b2d978b53f33e2d73e3e2febae939032a7897b3d78f1c` |
| Belgrade | winter | `Skylines/belgrade-winter.png` | `e5cf89bef5734d7dd41ecde360731ab78a8c69c582a580d4711d7947c26e522c` |
| Belém | summer | `Skylines/belem-summer.png` | `94311714062894fc433e4f16a1817278025a63bf52e64d1c46423159b1d35580` |
| Belém | winter | `Skylines/belem-winter.png` | `16d9d90c3c5c946e92d8c9d220565d96a2f00d73e9758c9a742b2381450cc88b` |
| Berlin | summer | `Skylines/berlin-summer.png` | `441fea9347e40fd9740c3a355531197623ef09c871674531d33c058dd8d45e35` |
| Berlin | winter | `Skylines/berlin-winter.png` | `688cbbfee0ba47103b836cdbab4d2572d99bccea635cccc25fe605858b11c071` |
| Bern | summer | `Skylines/bern-summer.png` | `2d715b81beedecc2d83f815f274b310c420c96f7cc3a405f62fc5682b8e39ffa` |
| Bern | winter | `Skylines/bern-winter.png` | `0345205a938a38dbbff9822a914a4d83cc0e83b4382c4f14f9d27f64f5dd3d90` |
| Bogotá | summer | `Skylines/bogota-summer.png` | `3f42432f65e5822e90cf04c05a82e410764eb22436559d2096db3650e8900b30` |
| Bogotá | winter | `Skylines/bogota-winter.png` | `1c344282a7aab1b1fa7acf486e7b0692cc5c296b205edb31375a1791566b7c41` |
| Bologna | summer | `Skylines/bologna-summer.png` | `5a915ef33f11647f9a205b1235fd8e15a52ab9a2a2651f4444e1acfc2cbdfb8d` |
| Bologna | winter | `Skylines/bologna-winter.png` | `c2d5db0133753db695eed2be86c9f2bab2aff1287b2424384e66af4e78c0e685` |
| Brasília | summer | `Skylines/brasilia-summer.png` | `65c282471316c3b72e2148eba962f4fbf6d1fbc2378d38665ab00eea2efcc155` |
| Brasília | winter | `Skylines/brasilia-winter.png` | `3f160ff9a99875314af7e6c9a7cbe21a6442544c3bd7481b48b0ed9dd3d96776` |
| Bratislava | summer | `Skylines/bratislava-summer.png` | `e39e0107f55acbf98195a3c1019aaedb2b58ed97a07c5e7da8dace2d57880140` |
| Bratislava | winter | `Skylines/bratislava-winter.png` | `7238802d12b462ad6eb94de2ca1fb85888214852f12249f0052391562a58b578` |
| Brazzaville | summer | `Skylines/brazzaville-summer.png` | `71219682186ffbe0377a961151c3d64d5106725b9f2f53893b4f802410eed8dc` |
| Brazzaville | winter | `Skylines/brazzaville-winter.png` | `1c4b3b3cec16cbed0f3447045afa816dcc89a16f03bd40e4264d14c5aa863149` |
| Brisbane | summer | `Skylines/brisbane-summer.png` | `cbc17c27069b584d21558f386b29746c00baaa2943abf20c5c12e973a62c1e13` |
| Brisbane | winter | `Skylines/brisbane-winter.png` | `262e157a5a2336ac3fdca17a8724eb1032f68ba71140a06ac2ca9ccb8f36e3fc` |
| Brno | summer | `Skylines/brno-summer.png` | `b6d329439a90cf19e3076cc591ccc5e59e17534843e500c93d013f696f1d0af7` |
| Brno | winter | `Skylines/brno-winter.png` | `193c50ae16b77955083239d3d5c7c19631fdc049007638ba846f6932e7071150` |
| Bruges | summer | `Skylines/bruges-summer.png` | `cf07b9b97f97d214836d7f426fcb8fb5bfe459152a8d4c28ef1a6ffa5f6e22e2` |
| Bruges | winter | `Skylines/bruges-winter.png` | `10a3dc0db3df0cae897a9174c49b2ea0314a5178fdc463c87016cc307d8ce565` |
| Brussels | summer | `Skylines/brussels-summer.png` | `893acc8d055c15fa7ddd8eb63c1713ab4c5920b7e59efdcffb651a66a4bbb137` |
| Brussels | winter | `Skylines/brussels-winter.png` | `74b5d56ffd4f4debc08dd508c2f25c420382ff29a612164c808b4f4c42fe7bee` |
| Bucharest | summer | `Skylines/bucharest-summer.png` | `1a57f923a4c2133b2aeb2d251c32ac47a5d9503c604e128399ca2a9a437822e9` |
| Bucharest | winter | `Skylines/bucharest-winter.png` | `72d4eeec1bf3a6e34c41c1190896ca848a879070efe8ccbc99b6fbc247398e7b` |
| Budapest | summer | `Skylines/budapest-summer.png` | `aaa97089564a057842a17ec97162baa098cc012d930bfa7a2b75b52f7a0ec59f` |
| Budapest | winter | `Skylines/budapest-winter.png` | `416b0593970ba68399e56cb0630d225b5e2aa9a71ad1c55eeaa408f99ed7920b` |
| Buenos Aires | summer | `Skylines/buenos-aires-summer.png` | `63bcd043708dfbc026071bff3990744f8c6b52c8ddef3f5082984b976d2a93c8` |
| Buenos Aires | winter | `Skylines/buenos-aires-winter.png` | `534099711493c8a9639b7c2de59a14d64e4f351039d8f770d377b5d5aec82325` |
| Cairo | summer | `Skylines/cairo-summer.png` | `3637bfafc84df48c28781d90becfefd22a1efbb0c009de63e6bb75de8f76b32d` |
| Cairo | winter | `Skylines/cairo-winter.png` | `e888f431ad0071e0e82723da7dcde7e925a0e6ed5d374735391b43ceb96835b3` |
| Cali | summer | `Skylines/cali-summer.png` | `c3db236fc9aa50d31cf212b77172882e1fb925c446c00da9b3e49f3ae3b2da1e` |
| Cali | winter | `Skylines/cali-winter.png` | `60454e673c3498f57bc2de2aea4581df3a55faaec64f3cc64e719c6c719b8be0` |
| Canberra | summer | `Skylines/canberra-summer.png` | `61095a553379201791256587c15c5015a82f267c8ffca4b87fa47300a0e843a1` |
| Canberra | winter | `Skylines/canberra-winter.png` | `448a1e10f0be1e7f0aa423e9a7cbda614d9aa00016de5a31326a76c16b147609` |
| Caracas | summer | `Skylines/caracas-summer.png` | `f760e4ce4045f560ed926269e4c05289c6911a30907d2beadb102e3958414052` |
| Caracas | winter | `Skylines/caracas-winter.png` | `a200536b43eeb65aecb146dd3f17dbe565a6387882e02a2366de5d287bf96b12` |
| Cartagena | summer | `Skylines/cartagena-summer.png` | `e465cbb689549025f9d503c0e95be5876b234d0c88cfecc3ce796f071e6151b3` |
| Cartagena | winter | `Skylines/cartagena-winter.png` | `5c0c1d68865c9318a042b16633cbf47d791a0e12127396cf68a0b4682f5bc286` |
| Chelyabinsk | summer | `Skylines/chelyabinsk-summer.png` | `40d799736551ddcd7e97041f756c52055979a72fbef8180ab3952987b0e774e9` |
| Chelyabinsk | winter | `Skylines/chelyabinsk-winter.png` | `30be58657cd295a1c5eab594c3c4131c5341a507b6febc58e4540c53e597af6d` |
| Chicago | summer | `Skylines/chicago-summer.png` | `fc33c94270c89cd19892f14b0b902b0384e2c345d6e1cda730bbefb0c4abc9cb` |
| Chicago | winter | `Skylines/chicago-winter.png` | `fd8711b19afdf6ae1587472c997cad431cd6466517877d197efb0d3db30f308c` |
| Chisinau | summer | `Skylines/chisinau-summer.png` | `752c95d9c91da170cc5837131a6e858cf0f03ce098218cc890263ca6da979cb6` |
| Chisinau | winter | `Skylines/chisinau-winter.png` | `d0d93c7cc8a7b9d0bcf6f839b1d1318a9c1a5664f1fb5eefcfd097c47ad00d78` |
| Conakry | summer | `Skylines/conakry-summer.png` | `f34e1eda48af6c5028d5f5d617d501f779ace8dc87dee00b5640018d1926d96d` |
| Conakry | winter | `Skylines/conakry-winter.png` | `0b5cc2e2c054a138f92e7df8ab4fc7a30b72ba0fca094f98d986dc57c8a66cd5` |
| Copenhagen | summer | `Skylines/copenhagen-summer.png` | `9aa3826ee195e580cab739ca35c3beb60395d1fb1c4a7e2a71828fec3c665f68` |
| Copenhagen | winter | `Skylines/copenhagen-winter.png` | `0493507e438af4e18cf7ff542b375538fe35af2a663e4ce611c82828e25d38b9` |
| Curitiba | summer | `Skylines/curitiba-summer.png` | `d43f7f7ad1a7fe1b701ad406964a0e1f4370c103654d5d3c59b7c629371ab0f3` |
| Curitiba | winter | `Skylines/curitiba-winter.png` | `6fc565ceaa2ef17db91c896868ea013fde30107e5be69c6304150b00d6debd27` |
| Cusco | summer | `Skylines/cusco-summer.png` | `9d55014656a9ca4607f386bcafbfb96b209118d7d1514c5263bd0846f7659ce5` |
| Cusco | winter | `Skylines/cusco-winter.png` | `f52b0ecba1992d2cc1fac8ed450c2cc92f2a0370b33f9148c5a371e805a2974c` |
| Córdoba | summer | `Skylines/cordoba-argentina-summer.png` | `8fe5d9ba6fc51f02f68d49bc8310afababfaf389cba6a3818e45527578342624` |
| Córdoba | winter | `Skylines/cordoba-argentina-winter.png` | `c3ccbdaf255a7f50989afebce5232e448596d4a0ec644a867ed3e28cb206e4cc` |
| Dakar | summer | `Skylines/dakar-summer.png` | `fdc2a8ea5d45c5588643909fa6258198925b415824d7ddc4998cad83c56eee4c` |
| Dakar | winter | `Skylines/dakar-winter.png` | `959d16c1877c26a413d52483be8aa40c81d4ed87d610de9b39b3c85518c829e2` |
| Dallas | summer | `Skylines/dallas-summer.png` | `d973cd0002c6f41644817f2545b45d134da11ec8efa6640416f0a06d5a0fc73c` |
| Dallas | winter | `Skylines/dallas-winter.png` | `5bb3c2868a43c6f9fcaa58fa3b2bad7f9ae227e0d1343864335537e26b8a2c53` |
| Damascus | summer | `Skylines/damascus-summer.png` | `50fed70962fd3c0043f7feb305f23ce241f5fff3112b657651077c4a865bd33a` |
| Damascus | winter | `Skylines/damascus-winter.png` | `e730f3f42774baaa81e33cadc5f7969f451b3df52e3fbedc32a34448668f16f0` |
| Darwin | summer | `Skylines/darwin-summer.png` | `6755eb1c01283ca136cc50dc8211b6ffc4cafbb1cbb76f2141686e3a5fb9eb6d` |
| Darwin | winter | `Skylines/darwin-winter.png` | `15ffa0ba97fe866eb1b453a1e5b93bf9f38b7b8aaec32075738f5edf45031598` |
| Dhaka | summer | `Skylines/dhaka-summer.png` | `db3589005db3e7a275115fc5934bb840c063db1d1fb93f5294b936266c035c6b` |
| Dhaka | winter | `Skylines/dhaka-winter.png` | `059ec0f3661d11cdc8c2ee791b69acd42132c615a98ceabce17ff6bfe68b38fd` |
| Domegge di Cadore | summer | `Skylines/domegge-di-cadore-summer.png` | `9cd513f99c5cd05b2902547f265b19593547f19253d6e43878edd5df72225036` |
| Domegge di Cadore | winter | `Skylines/domegge-di-cadore-winter.png` | `b460ae856010eebccebd614b8395e867886d6fd33c724ce99d041dacc6dac040` |
| Dublin | summer | `Skylines/dublin-summer.png` | `cb6499e0332cb4405c9eb6c2534c748e03b5d1d950fca88bb4915c7682b020a5` |
| Dublin | winter | `Skylines/dublin-winter.png` | `6a1505415a039fab7690ceff8cd4c764025821b9de6bf4ba39ce5ba7d76bdefd` |
| Edinburgh | summer | `Skylines/edinburgh-summer.png` | `119c1415010ed284a497c5217fd08cbe52e11a4f6b9a19ea3927c34953399322` |
| Edinburgh | winter | `Skylines/edinburgh-winter.png` | `062244227385d270d742643ff8b9a90ac74da90a66e700ddbaa9c9e2ee7a1b3e` |
| Ferrara | summer | `Skylines/ferrara-summer.png` | `305064267d4d9f2c76a28f849f4f69ca294b8f7cc42518745560b26dc84a93f1` |
| Ferrara | winter | `Skylines/ferrara-winter.png` | `9122503d14dfbd40c993e4fbb10f6b565de91d09931417b0cb75db2c686ca764` |
| Florence | summer | `Skylines/florence-summer.png` | `22af93d01b1dbfeed18c2dda177b265c63b0c2fc6b2c24c8ceb27eb661c58add` |
| Florence | winter | `Skylines/florence-winter.png` | `91ea0f946f368adbce7932cc35b5afda1d65dbef583397da22c9276c7b6545f9` |
| Fortaleza | summer | `Skylines/fortaleza-summer.png` | `fb1ce62c4a3e42d179e9eeee221903fef675b1fc76915cad4afaeb096dd10cd9` |
| Fortaleza | winter | `Skylines/fortaleza-winter.png` | `cae1955cb6feb15411d8735135d9affe889d728eaae6c3691a53e4d350de9b76` |
| Gold Coast | summer | `Skylines/gold-coast-summer.png` | `7c56b8a71144ec78377d9f62c2ec0443e0fdca94d2551af436108d16e960d5f1` |
| Gold Coast | winter | `Skylines/gold-coast-winter.png` | `8da13e5d1513b4323ad5cb56630d3ad51f65d0f867d432e98eb0c68d490fdc3a` |
| Guatemala City | summer | `Skylines/guatemala-city-summer.png` | `e1cfa1b1b91483f5ec562b7b64414aff1a7f02bc491590b438528d8d5a43ed00` |
| Guatemala City | winter | `Skylines/guatemala-city-winter.png` | `7613c5c217a4269dab2929ad5680a7a90b305482cd6298170f47b266193f5960` |
| Hanoi | summer | `Skylines/hanoi-summer.png` | `c6761da199bc2d0a9febd6e70eb10587448a30121e141af9f1eeee6f90eb0db4` |
| Hanoi | winter | `Skylines/hanoi-winter.png` | `6dbe14fc61795e361ddd720fa42cc6317cb374250a7343f6b48e2ae0129c036c` |
| Harare | summer | `Skylines/harare-summer.png` | `ee0be26bbe81bccf23284813762025f85870fa088368d77212fbe40ee59cb3c3` |
| Harare | winter | `Skylines/harare-winter.png` | `a8cf64ad3177537f6406ea052a6ba32cf7d90932f3df538b0da8bce01ca05dd3` |
| Havana | summer | `Skylines/havana-summer.png` | `c9ab0e9aadc4a19295fe1144f51324039cd55875fb890ca650ab9650c762c765` |
| Havana | winter | `Skylines/havana-winter.png` | `2c226d6a6326e4c421f90b6f74340e5ef589402c34d3aebb4a1fecfe030b3dc2` |
| Helsinki | summer | `Skylines/helsinki-summer.png` | `2e480b5e325d46aba6765332d7dbc21844961f13e0ba702092ee84fa880f766f` |
| Helsinki | winter | `Skylines/helsinki-winter.png` | `d04a52b4af05eb1f4083b5455dd0a8577434e4e61994f3ca4050a823789ba46c` |
| Ho Chi Minh City | summer | `Skylines/ho-chi-minh-city-summer.png` | `73f70c855044af3e20049d8ce80d570f8fcb6384a528c815ec28eb5e3d347dea` |
| Ho Chi Minh City | winter | `Skylines/ho-chi-minh-city-winter.png` | `3d03ea0d8600222c141c99d666df046832899f9d19c5fd2c98844fc5f5c893b7` |
| Hobart | summer | `Skylines/hobart-summer.png` | `18d64c064c636dcf4116d82775b2a7f6fd7da179f958444addf4b07789e97e3e` |
| Hobart | winter | `Skylines/hobart-winter.png` | `23596b300af2fead572044b6972822312772d05311fff9265068c92863a04014` |
| Hong Kong | summer | `Skylines/hong-kong-summer.png` | `b231288b75a5410b30641d40a393df417c2a268362616edd3cfc5b7c455bdcd2` |
| Hong Kong | winter | `Skylines/hong-kong-winter.png` | `0b687d8ade820daa1984001e3f835dcb78a17cc1597311be2b75b1a98a3b8e49` |
| Houston | summer | `Skylines/houston-summer.png` | `9582a630903278dfa85e226a185e6d3e4f49c7c8b2a5ab1fd293996a03c7e522` |
| Houston | winter | `Skylines/houston-winter.png` | `a9163a9eca63854ccf8519fff67fae59a81e173d9d2949dbc8adf1665839f7ec` |
| Jakarta | summer | `Skylines/jakarta-summer.png` | `a89266f0e0153f05e7358509d18360fdcabd2cf480948bdb579d06f7a471d839` |
| Jakarta | winter | `Skylines/jakarta-winter.png` | `55f8debf70c88182be2aec4992192cbfce6bf0c0c333f06babf7e0eff6defbd2` |
| Kabul | summer | `Skylines/kabul-summer.png` | `292da53a56044591ca148f9b629e1add92f5f40e34a2632c13cb755945519533` |
| Kabul | winter | `Skylines/kabul-winter.png` | `bffafcf93001b8c615d8390b207ba171acb6b89b3b0636a66f7e48419b8dd70d` |
| Kampala | summer | `Skylines/kampala-summer.png` | `aeab00fc2c980704d51ce4d8a68773f5271338d983fee5d8b6a33e6465c9cbc5` |
| Kampala | winter | `Skylines/kampala-winter.png` | `ecb2a016f674f92bdbd7790ebbd14ec00b4c5015f9b3d44955d31ef3967a344e` |
| Kathmandu | summer | `Skylines/kathmandu-summer.png` | `94242dfddf8130310244ce276f8f8f04779a0b4ab2479ce5b056f0a5ff72b7f9` |
| Kathmandu | winter | `Skylines/kathmandu-winter.png` | `738565dfa887fa567a0b748f25f15526c87cdba550380b94d62cd1171fa8bc62` |
| Kazan | summer | `Skylines/kazan-summer.png` | `641b2b516bef6394f10c6218848d3643722648ea4ee19d4528d5a4e79e8a91d8` |
| Kazan | winter | `Skylines/kazan-winter.png` | `01806c217b5c47c3086d66215be32e4d5e530fbaa202f6f39ed16213b02e9a17` |
| Khartoum | summer | `Skylines/khartoum-summer.png` | `0e7632c23d34e920f9cf5e7e20e22393b8616e140681ef62b954c126d4e1d96b` |
| Khartoum | winter | `Skylines/khartoum-winter.png` | `97be569809e5a1f1ba3da1e92ad15cc7fe2cac2e0e5b69180132aa96a6cc709f` |
| Kigali | summer | `Skylines/kigali-summer.png` | `25a5d8e35546f8a0e71776c7bc2edb52a82e6e221ce18aa6e9f460fc71ae64b0` |
| Kigali | winter | `Skylines/kigali-winter.png` | `7bf2dc92d2e85ae0f7db10513d4a7ec30f767b51f531cd9366cdd87bffa7be0d` |
| Kingston | summer | `Skylines/kingston-summer.png` | `e5cafa0d17768417530b406a237932d0f19c0607e339b91e8de5e406ce9c0549` |
| Kingston | winter | `Skylines/kingston-winter.png` | `fbe81bbbe4f13e9a4628b11f58e3582c43e22441c5502cb2df54ee11871057a4` |
| Kinshasa | summer | `Skylines/kinshasa-summer.png` | `15e2b61685fbda7f6c395e40198999423aef4ede60a3980b4d8b2667b63f6753` |
| Kinshasa | winter | `Skylines/kinshasa-winter.png` | `a7312d297189274065b9a8f212bafebd7e838aaa6752522da92b2cc3b86a3bad` |
| Kraków | summer | `Skylines/krakow-summer.png` | `58c00559ee95420c0b9cc712a0189c6a24a5ff5750cdd97192611da0f1552b30` |
| Kraków | winter | `Skylines/krakow-winter.png` | `4d72efc893261a026d2cf49b2e3f7715a9ebf7a4104261c011e55ee41e5ff52e` |
| Krasnoyarsk | summer | `Skylines/krasnoyarsk-summer.png` | `8f683c2b5a9e716d4d0d553dc7890a43c2e92a16862de748a627d1d7241f566a` |
| Krasnoyarsk | winter | `Skylines/krasnoyarsk-winter.png` | `7f31e617f507123c63f0f3db1baf8e7836eb6c3c7666f5138383e648ed5aea5e` |
| Kuala Lumpur | summer | `Skylines/kuala-lumpur-summer.png` | `c5ab3eb55f17322089c7c5a41134cc35be9580fe04041c7361a011de7d43497a` |
| Kuala Lumpur | winter | `Skylines/kuala-lumpur-winter.png` | `4168bb4ec1530828f6405eff3e922aab75f82f2f94fa91bde4e88983004b6ede` |
| Kyiv | summer | `Skylines/kyiv-summer.png` | `982b25591266b18f656da9539f308093d940ee75eb876ff43b19191fd814486a` |
| Kyiv | winter | `Skylines/kyiv-winter.png` | `661247e68b3b1019e34b12a7188e8b0f1c30c53fc29c22d637df2abbda84f4d2` |
| La Paz | summer | `Skylines/la-paz-summer.png` | `766919df5b4ab6a176b420786c23d8cf2ee104bc6e26df806c436e6f933c6a27` |
| La Paz | winter | `Skylines/la-paz-winter.png` | `1d8e82f9f9fe383128bc8bddf9f7c44d01eff3d3fb4b0876cfc6446cb1cfc466` |
| Lilongwe | summer | `Skylines/lilongwe-summer.png` | `8c753fe48e09c321b97e170caec7c30709c4ab87cb1a4854eba724b5aaec7b53` |
| Lilongwe | winter | `Skylines/lilongwe-winter.png` | `ddc8bd99489d7ca25635ccb69873f02aa5fb396d4d4276447027240c1e76ca9b` |
| Lima | summer | `Skylines/lima-summer.png` | `d13f4a368539c878f96dfffeb7b4ab9cd0d19407a50abf4043d67a12d02337d3` |
| Lima | winter | `Skylines/lima-winter.png` | `3c489297405d2d3f4a32fea92e0c38f4ec9f286ad4b81d8feca08cd27db59b78` |
| Lisbon | summer | `Skylines/lisbon-summer.png` | `439390c6f3120d13996ba75fb483ad00e4c6cd5f8a00e67411cedf35fe68883f` |
| Lisbon | winter | `Skylines/lisbon-winter.png` | `7419c4800903bb2ef2531cdc4cfef2e364798675d844dda1c0af516c720870a0` |
| Ljubljana | summer | `Skylines/ljubljana-summer.png` | `57770e3077cce7f6b67b8c7342cc61181df5f9c3b419d5ebd424fe41dbc3d176` |
| Ljubljana | winter | `Skylines/ljubljana-winter.png` | `025eb41d8abd93465ee245c8b975635b074b55f873a7b6c0ffb0ee2619b0cf82` |
| Lomé | summer | `Skylines/lome-summer.png` | `740b8d5e5756b0e42607e0db74412dc334531026b3e246bd0ec6ba896421633a` |
| Lomé | winter | `Skylines/lome-winter.png` | `899a4c8d0cf3c732cc08f24b0726d3f748cd1624a8f839ea5a3e79d9a0c471bb` |
| London | summer | `Skylines/london-summer.png` | `31764f4c139d3a648edacc48c2881c97d3b0594aba6f6dd31db982b62552c549` |
| London | winter | `Skylines/london-winter.png` | `36067ab7ba244ff9285efe40fa564324acb3732753b472c1b2cf14cca34e881d` |
| Los Angeles | summer | `Skylines/los-angeles-summer.png` | `0335653d0c174b335b01a40dfcff0c2336f4acb14aca7ee614ec4fc5dbcb8a4f` |
| Los Angeles | winter | `Skylines/los-angeles-winter.png` | `29a41303ba9537d4a6c4dedc2ddda1a40ad2f10b0ab4b8ba84bc92bc531b964e` |
| Luanda | summer | `Skylines/luanda-summer.png` | `a309c66217a3371b9307ec2e683b9f6e3f96778c0e6c33be0b82734cb1eb9387` |
| Luanda | winter | `Skylines/luanda-winter.png` | `f41257911385ab12227893f7ad7888903d1b3054572819fd9a34268b3c71d1e6` |
| Lusaka | summer | `Skylines/lusaka-summer.png` | `ffb24c0da72472026f30d8e13e3a59499367169b6adbcdad51af12ef91052f5c` |
| Lusaka | winter | `Skylines/lusaka-winter.png` | `45af1251c4ef7175045538916fb2739df07bd86c13ad4f6517b912bda43c3814` |
| Luxembourg | summer | `Skylines/luxembourg-summer.png` | `13326ba5ac9617e94709d74604bb614fc7f98130ddd3dc0f1c829be080ecea90` |
| Luxembourg | winter | `Skylines/luxembourg-winter.png` | `b7687a6b2d75d016e2d85993e267d265858b6ea8f49013f9a373b05555623fc3` |
| Lyon | summer | `Skylines/lyon-summer.png` | `13fc2c409cb005fd106720a77300f1000a06c32bc4010048c1a405343a0f5e37` |
| Lyon | winter | `Skylines/lyon-winter.png` | `698183f69bbb95a7d82beb55118ae04fb6915c5857003dbbe62acd6cd0f0ad89` |
| Madrid | summer | `Skylines/madrid-summer.png` | `cfe9e1ae56224ebe97e6a8e9e795ae49888091693cb7c344f2c2242c3b7f5081` |
| Madrid | winter | `Skylines/madrid-winter.png` | `6e4bd28d43af478dcc5d069ed8eb530aab9e2f804ca7bf4427af6e9d34c3e044` |
| Managua | summer | `Skylines/managua-summer.png` | `4f769bf4194d6f0c77f31cdda5d1c7e101b16aa3f175bfa03eb35f5e0118eaa4` |
| Managua | winter | `Skylines/managua-winter.png` | `0c94387f0534f9a1a9e4472cdea994a0b4e32f7ce1d5e38a6a30cade82be5737` |
| Manaus | summer | `Skylines/manaus-summer.png` | `87e1a872ad0e2aa4253123f98bba010ffbeeb0961bdf497400cd7f63ad925cad` |
| Manaus | winter | `Skylines/manaus-winter.png` | `93537ab1246973735381bc512370a892b9ea91cb5284a8001f5d3137441759c4` |
| Manila | summer | `Skylines/manila-summer.png` | `f6bd5b09ce12b8991bf03adf4c44d7bd1a2ab43ee104f5064af2f45e9b10bcea` |
| Manila | winter | `Skylines/manila-winter.png` | `c79ec76fcce7fc4d659ea1d211a6154b7ee3054e719b04848808c9c05577cbe1` |
| Maputo | summer | `Skylines/maputo-summer.png` | `4b154cff55de5f51f345ac5d1a0a3430814ee955ec75b353bdeddf2cca2a457d` |
| Maputo | winter | `Skylines/maputo-winter.png` | `b3da76a1921a13b7ac6e720a9ac2978569888831e87baa9e6df69dc0845434d5` |
| Medellín | summer | `Skylines/medellin-summer.png` | `5f4879b3b88fd80003d14ea6eabc819ddce5f901e032bc5a8452bbef95e4f17d` |
| Medellín | winter | `Skylines/medellin-winter.png` | `2d1ba5f9ca03bccfb59e29a3fd57de185e8bf3b6f4494b19807080ec00c258d0` |
| Melbourne | summer | `Skylines/melbourne-summer.png` | `580afbfa40653713de3b6b57063569085cc3c205821340e7a8925a9c99acff0f` |
| Melbourne | winter | `Skylines/melbourne-winter.png` | `d2f711b545eadab94a8ac4e9796ed82f1a08b6a3c2123293620011bd9b08b6f4` |
| Mendoza | summer | `Skylines/mendoza-summer.png` | `7819946dc5dcd2ffd49354024c098dd5d7c29427d422945fbba355c334770054` |
| Mendoza | winter | `Skylines/mendoza-winter.png` | `f5cd5170c48d0d9b6d2fb4447238d132f837cdba38b33ad6f3853f9b7a99306d` |
| Mexico City | summer | `Skylines/mexico-city-summer.png` | `26ce42e6b0addd7bcc9a1f3ea66d26206170b855ff98e40fce0ed71692c401b5` |
| Mexico City | winter | `Skylines/mexico-city-winter.png` | `f15bf6d062425c1935586fa8c42ab689eec695a4b4b9717cfdc34f7934d85792` |
| Minsk | summer | `Skylines/minsk-summer.png` | `5fb73607f76aa8f79ffde55640e608173ca4248e826b994b28934d27e08e5383` |
| Minsk | winter | `Skylines/minsk-winter.png` | `956d71ae37f5c2adb8aec10e3216f0501a2a7d3692e9412768f17fa5e25bfb9c` |
| Mogadishu | summer | `Skylines/mogadishu-summer.png` | `fdeb18248f549ca1c1d7456a3c1993389720ecd8af0a9a8777556c6b4b9a3298` |
| Mogadishu | winter | `Skylines/mogadishu-winter.png` | `8bba630f01adffd28a12cd5225a47f8a6f05b40bfc134cc0b028990bccb7743d` |
| Monaco | summer | `Skylines/monaco-summer.png` | `8108a9c65a3d86eb8298a8c177b7a2cf3eb791d00ee657837e679e39ef98e081` |
| Monaco | winter | `Skylines/monaco-winter.png` | `b2afa1fc24b8acd1319be4f0cb30b5bf7e6bfcda056d2f3489c850d09e411414` |
| Monrovia | summer | `Skylines/monrovia-summer.png` | `1c592ae80e520d5dd171059f04e02d26028c8392e458c55c4f0c779c5e3049da` |
| Monrovia | winter | `Skylines/monrovia-winter.png` | `f804f66ad779ac38ed3971e506046b599c2abd98e40328ecf708e08a4bc9a002` |
| Montevideo | summer | `Skylines/montevideo-summer.png` | `1064718694871ac727a2b245fd160f80442b46e70918ca89f4e5932a9f48c89a` |
| Montevideo | winter | `Skylines/montevideo-winter.png` | `ba0579568175cdacdea6966143848f884abbe718a976027834256989ad5cc0a3` |
| Moscow | summer | `Skylines/moscow-summer.png` | `7f835bf55240d7b59c3df1e7663d7ab7e14c55b759ea355dde0535ce85c7a992` |
| Moscow | winter | `Skylines/moscow-winter.png` | `ca29c28c5bd2dbef3304b5622c358a4b281a59ecaad937d6511dcb98af99120c` |
| Mumbai | summer | `Skylines/mumbai-summer.png` | `deaee34febca8a02bed368b971f53cf944513c8b2d7ffb54878e4ea11034b6be` |
| Mumbai | winter | `Skylines/mumbai-winter.png` | `3c0a28eb799aebef6e93111bf02945ff82d97552565287e87ea4d77366492de7` |
| Munich | summer | `Skylines/munich-summer.png` | `2c42b6dec69fd9df2151d2c96db8902dfff8625239e300553f76af35b74c5cf6` |
| Munich | winter | `Skylines/munich-winter.png` | `17604f66b558402f89d9ad4d74a257390d4437e148792a2f9d0f3103604e1019` |
| N'Djamena | summer | `Skylines/n-djamena-summer.png` | `51306ad9d3ae3b0c60f33c4b24526858f2e3238a7633554e7d040fa19154f75c` |
| N'Djamena | winter | `Skylines/n-djamena-winter.png` | `a4dc2e6732398cb48e42e908398b6aa1162304fd620fc046ec88350e495924c7` |
| Nairobi | summer | `Skylines/nairobi-summer.png` | `35f81b6a206a1c98052f0eb3046ef0f0c94153d67e0cabe2fdf4f7ac734f78a5` |
| Nairobi | winter | `Skylines/nairobi-winter.png` | `5dd78fe0bc7b437e512a7ebcedf0b76abe4923d3085724594c260a6816779282` |
| Nay Pyi Taw | summer | `Skylines/nay-pyi-taw-summer.png` | `b4c37393402a0f5c6713dfff72aa8340c094f82dd925d651641f1da81fbbe649` |
| Nay Pyi Taw | winter | `Skylines/nay-pyi-taw-winter.png` | `8cde2337aaa97bf03353ad6c933e267fdcf4c061b52a23687a9faf10fad3675e` |
| New York | summer | `Skylines/new-york-summer.png` | `153d1b1a2855cf748913d233c19b20fe93157be62cf2bf28412b01742048fb83` |
| New York | winter | `Skylines/new-york-winter.png` | `228417919c525953429b3fdf820855e1dcd2519c4536813783119b15bb99eab8` |
| Newcastle | summer | `Skylines/newcastle-australia-summer.png` | `e380ff48be4a44cf1980cfe94cc4662e72776b6dcb421e1425a38cf5fe401743` |
| Newcastle | winter | `Skylines/newcastle-australia-winter.png` | `868b84cba40fe631855c456fc3a973116a19e4de27531a97e80e63ff91caf5b8` |
| Niamey | summer | `Skylines/niamey-summer.png` | `e1efa4ace3f0b214521d36eb55f917f29e3b0d7290c224da56e5846deeadc3da` |
| Niamey | winter | `Skylines/niamey-winter.png` | `76cdadb7ec138d6a3aab52277bfeae71d5e47d528b6bf573a307f6ffa6bf7a02` |
| Nizhniy Novgorod | summer | `Skylines/nizhny-novgorod-summer.png` | `5b616b22da55ae5cc1542a0efd27c06cd3a8500d97852fddc4bbcaefbca76af6` |
| Nizhniy Novgorod | winter | `Skylines/nizhny-novgorod-winter.png` | `92d0d0ec19f1a9125bffef334ce537f1c3a299400a0560b7829620a091049133` |
| Nouakchott | summer | `Skylines/nouakchott-summer.png` | `c2817351fa87dc1c4e9dc16ad5cfc52b75398794d4b37f2fe390c4e355921acd` |
| Nouakchott | winter | `Skylines/nouakchott-winter.png` | `afff806cd9edcca39865f4078e3e60655340dfe55c1ea37ec9ade08930c2b1f3` |
| Novosibirsk | summer | `Skylines/novosibirsk-summer.png` | `7fd72522378192cdd2bd37c53b2672675eb32f9e02e7a83c95b2b1027413fdf5` |
| Novosibirsk | winter | `Skylines/novosibirsk-winter.png` | `94f422af948ee8b2bf8e6ff4871a4bd59c5ce69ca2d454b257f201389ef15e03` |
| Oslo | summer | `Skylines/oslo-summer.png` | `8703b4a9253824b1a2cbe4364109d6a86756ca6d03f72819cb8e2ba0344bb1b8` |
| Oslo | winter | `Skylines/oslo-winter.png` | `1e8eb4b893b7d323a780e4279a2e3402348da3d66e52c26c0e69f00d5c3a69c7` |
| Ottawa | summer | `Skylines/ottawa-summer.png` | `e06bffa80743b50289c974f13175286e57eacae52a62f22808c4b0a40bb73504` |
| Ottawa | winter | `Skylines/ottawa-winter.png` | `269ad645642c0f70df63764c1bc6e64df1e08f84a5905279034e67ad92f3ba42` |
| Ouagadougou | summer | `Skylines/ouagadougou-summer.png` | `436171a6612531c90e8c9ddd2db58bf630eb4287e6a84af5515651b023779b88` |
| Ouagadougou | winter | `Skylines/ouagadougou-winter.png` | `db19f3aeb9fb3865dc1a0bde5153936541149b65bab2f83cdf677cd7a2a76944` |
| Paris | summer | `Skylines/paris-summer.png` | `333b04c5af8297cb12136b6dc59bb3a41a44213b2ff637704b4f31f16a3b5673` |
| Paris | winter | `Skylines/paris-winter.png` | `5c66a553151cf6ca968a9b05591a05f5836026c01a7ebb602718e5b761109282` |
| Perth | summer | `Skylines/perth-summer.png` | `58f6f304389c6868c93150ffa278fee35d13221f0593c4eccb33deeaca8b43bb` |
| Perth | winter | `Skylines/perth-winter.png` | `7e7255669b5ee0543dd890597eb5c2d36fd751d04f069fc2a16d6e0bc1834bd2` |
| Philadelphia | summer | `Skylines/philadelphia-summer.png` | `21c242faa4c486f214badf4449023396d210c3c9c24978bb3af67669dff94415` |
| Philadelphia | winter | `Skylines/philadelphia-winter.png` | `05c16a64fa3993c6f875c15f8b3a588ed512919ada526b6b8d7acfae085cd151` |
| Phnom Penh | summer | `Skylines/phnom-penh-summer.png` | `54f657ab1b483001fe308f4352b34963a66bb32392826d2d08f426d093213675` |
| Phnom Penh | winter | `Skylines/phnom-penh-winter.png` | `b51b9af3740228646724d6f556211f1a0dbf42f76d98b5de42aa3463db25f6a0` |
| Phoenix | summer | `Skylines/phoenix-summer.png` | `978399bc31327bf35489c795b52882c36b5c83141efd00824564523bfa49dcc3` |
| Phoenix | winter | `Skylines/phoenix-winter.png` | `d14c32cddedb726ebe49ccb8f30da624f7d3496f92b7a7506d6919cc9bd447d1` |
| Podgorica | summer | `Skylines/podgorica-summer.png` | `5c075c4012702874e34da96df0708ce08ddea3a588fbb482fcecd6952bba779c` |
| Podgorica | winter | `Skylines/podgorica-winter.png` | `2d07774d93de3bd63c0baa278145b10dc0e689bb4119f39f07de673cfdcd78d7` |
| Port-au-Prince | summer | `Skylines/port-au-prince-summer.png` | `94d953d582b395ee08eeabb7760d2394f2d18084d92ed8593b06d65a218a05d5` |
| Port-au-Prince | winter | `Skylines/port-au-prince-winter.png` | `0eeb1421a268e4e328a5cc86b2fdc7bd6165dfab71ef377bf8fa16e3d8f81afe` |
| Porto | summer | `Skylines/porto-summer.png` | `9b3d85400b0905b1ab29ea57d81568c071ec0217b61c841efb29b3057fe9ced9` |
| Porto | winter | `Skylines/porto-winter.png` | `b826d66eca5e0951a227d4882ef9586f0e7d903e7769f5997e0ab51f3a68df9e` |
| Porto Alegre | summer | `Skylines/porto-alegre-summer.png` | `3f8cb23e220f3d1c43b0b42a94b2ea3250246c35a9e9691c76dff8d93ef32652` |
| Porto Alegre | winter | `Skylines/porto-alegre-winter.png` | `bbcfd7aee2e88b0a8840bb9253a273682c66604039cbd93a96e7a157d3dc8709` |
| Prague | summer | `Skylines/prague-summer.png` | `dc8d7314289007b6085924765bd863f4213aab00188f20bba39e2bc0a31d773c` |
| Prague | winter | `Skylines/prague-winter.png` | `78ba909127ea67157c380e284d23627208d1a82a7db9364cff58fb2ed26cdb60` |
| Pretoria | summer | `Skylines/pretoria-summer.png` | `1f6d6b1e827b585b2f2736e43ffc515749b3f423a99277613abcb3a16d5a9241` |
| Pretoria | winter | `Skylines/pretoria-winter.png` | `f6d6e5e58653193391b04d03015608a750fd17bf257703e2da3f9ef0fbbaa670` |
| Pyongyang | summer | `Skylines/pyongyang-summer.png` | `87e134098992e60936adbb073c31c66bf03dcebf6a3bbb1ceb3a4dee419ef907` |
| Pyongyang | winter | `Skylines/pyongyang-winter.png` | `f8a42fe40a97d193a510f0acce420297269e773ab2a95ece805aa6daa704fba6` |
| Quito | summer | `Skylines/quito-summer.png` | `26c082f4e85faa6afd5d23468eb05c6a259714367561c4a97f9d80a7d4ddae15` |
| Quito | winter | `Skylines/quito-winter.png` | `23769ce083dfdaaa56f2dfc0cb2c32de87b4cff14ecb59c96435a4427094a111` |
| Rabat | summer | `Skylines/rabat-summer.png` | `e00cddb77d5ee9f5c53716a5ee2cbf6c0a18b431a87256a1c717dd2574645972` |
| Rabat | winter | `Skylines/rabat-winter.png` | `7b1d01bc750a73e5962a86d86c573fe67724e2315cc07c41176dd28188285c31` |
| Recife | summer | `Skylines/recife-summer.png` | `f270b457df5b3a2d54225945cd7fe49158f64654e861a58294c8077cd0443139` |
| Recife | winter | `Skylines/recife-winter.png` | `d63a5e3397140745f6df8433ba3dcf5a891e0a52b2840741a5229c843874c96c` |
| Reykjavík | summer | `Skylines/reykjavik-summer.png` | `d2e4a7109096a9f39c16983937e169a9a5a330e94989237da264caadbea78c5b` |
| Reykjavík | winter | `Skylines/reykjavik-winter.png` | `d2c946db40025797917575314a92ce2e5b79cda5e7ee14c8b59fd25a25c5f6c3` |
| Riga | summer | `Skylines/riga-summer.png` | `90f2da5a517b14c00e1186e0ff7db6c84815b4bfd45f1748c7d2a2fb1e2ed23a` |
| Riga | winter | `Skylines/riga-winter.png` | `4605e636a76af844406670756228acb90ddb3e0d3812f12bc93b5362e4a45776` |
| Rio de Janeiro | summer | `Skylines/rio-de-janeiro-summer.png` | `5366be40619b0163e0b6596d870ad2b905ffa045feec10ed1d17772f05941758` |
| Rio de Janeiro | winter | `Skylines/rio-de-janeiro-winter.png` | `e619161eaca5d5ec023f5e099f33eb7f846fa3b1ae8a966b1edc8d9d671bf932` |
| Riyadh | summer | `Skylines/riyadh-summer.png` | `fe044608fa1ee1c9a35534009e3fbf4a1301f8fff62dd58bca15ddc837e1bca8` |
| Riyadh | winter | `Skylines/riyadh-winter.png` | `f4e0e5c81e8768525814d5dffb15d417c166faaa3364c7330c5ae6f9815c48aa` |
| Rome | summer | `Skylines/rome-summer.png` | `ec5f7d1d1a6c0cfe362635c3597dab4ae46a91eaee5159e5b3de6ae51fddab7b` |
| Rome | winter | `Skylines/rome-winter.png` | `dd0636ab9c4d221a89e98e845bbe0792a23a02db8f141c383c10a80cd060f9b8` |
| Rosario | summer | `Skylines/rosario-summer.png` | `4109ef2d12dadad0b6dc0d3bd557fd8fcb1827758fe16978a5f95b8bfb2e3b9b` |
| Rosario | winter | `Skylines/rosario-winter.png` | `4f317cb5dbe992c9da1fff6fecb7718e4352f4b785ea741510ae53547979f0bd` |
| Saint Petersburg | summer | `Skylines/saint-petersburg-summer.png` | `93e3685cc35859a3fb26da2ce661d1a63c5b224ff77bbf2918ff12a288d07ada` |
| Saint Petersburg | winter | `Skylines/saint-petersburg-winter.png` | `e296baa1fa1ab59e4cc23f6d09235fa71b2dfbffe4eee22877f6dd4a155ed636` |
| Salvador | summer | `Skylines/salvador-summer.png` | `65082e7903c67e7c971475705a422f2764dbd5b224db630bbc4dfa6602a2db87` |
| Salvador | winter | `Skylines/salvador-winter.png` | `ddaa819c0c93d43bed6ded152475d745b18edb80dcbaf46f39363e9d55a07abd` |
| Samara | summer | `Skylines/samara-summer.png` | `2509567d78f91674865de5963ea214a15eca449d8ceca3f12d956b5fef180dae` |
| Samara | winter | `Skylines/samara-winter.png` | `771f6b1a1f4f193ee4ef0eee7a44e0e99b0fb90f6a34d6fbcdba258673ebcac0` |
| San Antonio | summer | `Skylines/san-antonio-summer.png` | `99cc3fbd572443b364426b805ad84a0ba637c5c37e84706a70a7f992d7a8a323` |
| San Antonio | winter | `Skylines/san-antonio-winter.png` | `5c53a21d012f2a3083d1327eca0924da85835c52a29dcc129d6238500d5fcd0a` |
| San Diego | summer | `Skylines/san-diego-summer.png` | `1098358373b9b204c63fdef64604324f890cdf068d545e00f09a97fed3909940` |
| San Diego | winter | `Skylines/san-diego-winter.png` | `8bd918d4412aaa8730ab5e244a02bd7e293af1031bbdfd076e72b1d1706d9eef` |
| San Jose | summer | `Skylines/san-jose-summer.png` | `66bdf10bb5f1259711047b3aebd827c61ef8019eb51bb89841e463713696bcbb` |
| San Jose | winter | `Skylines/san-jose-winter.png` | `93943528cfb75a2b12edfc76a956b83479bf1f0725e66fd0748dfd5aac8e992a` |
| San Marino | summer | `Skylines/san-marino-summer.png` | `d0f1019ffa1055fce3e8ff61db488ce26421fcc614c6eddcb1db44a4dc710b5f` |
| San Marino | winter | `Skylines/san-marino-winter.png` | `1ad57bb8dd9cac40beefaedcb5e46fc39be29ad17399f845fed6b10d4d890491` |
| Sana'a | summer | `Skylines/sanaa-summer.png` | `4b424b79e3c18b0004349a8e0c0e4eb7bab7d6d91b4fcc93ea8fa2c37ea35c95` |
| Sana'a | winter | `Skylines/sanaa-winter.png` | `cad38e6b4e5d3e83ce776e20eb455b87dd6696700203a4989d5662f38cf12ebd` |
| Santiago | summer | `Skylines/santiago-summer.png` | `93dea510ce269dba18ee08e7ed66e8945a207cfadb7312a2fc505969f3f4c0f0` |
| Santiago | winter | `Skylines/santiago-winter.png` | `5ba9345eae123d529556c82e58fd280451ee677b25c2484bd554fd5ae47b8e49` |
| Santo Domingo | summer | `Skylines/santo-domingo-summer.png` | `1e40f35dd82713c8b7a36beda2bcca6b3dc1579371b86a94910a96a20eb3dcb6` |
| Santo Domingo | winter | `Skylines/santo-domingo-winter.png` | `246129d88f6f4e9835e5d678ccaab80e538ede9dbcde7262cc1e5ce47e7ff9e1` |
| Sarajevo | summer | `Skylines/sarajevo-summer.png` | `25b2d75d73ec91b33ae1ca53c98424c06e82c60c288dbbeec2160bd8ef261034` |
| Sarajevo | winter | `Skylines/sarajevo-winter.png` | `f1b9ba13f399ad01ac87804b06c31aae1efe0fddad3f9fb213dc0e3a3eb68973` |
| Seoul | summer | `Skylines/seoul-summer.png` | `b299692f5cbb482f38b039fabce0a18696f3ba38283c3b4c0c579ec9405bbc63` |
| Seoul | winter | `Skylines/seoul-winter.png` | `65da9f52fc1ec292ac96d8fe2d0af8c61359c0f4d5c7a8b07d4489410d9fed9e` |
| Seville | summer | `Skylines/seville-summer.png` | `55dd435008baab9e7ebf0c786f68f90f39cb4588cd36639c3bf458693ff3c088` |
| Seville | winter | `Skylines/seville-winter.png` | `540c36374ac45bec43254b666934a40e9723f1cc7d481934d06f0d54604df8bb` |
| Singapore | summer | `Skylines/singapore-summer.png` | `d3dfcd3eecb0a65c347e183bb31bc3cddd3799b2d6b80c027bd67ab08a05f9e1` |
| Singapore | winter | `Skylines/singapore-winter.png` | `8eeaca00e2d140df42e8c27f74caffd32ffbca14e39445ac7b6654a203aba5d7` |
| Skopje | summer | `Skylines/skopje-summer.png` | `2e136114c8205699c74c2ef89be9df8d35dd6127a370a58d5dd7e28098dab9f3` |
| Skopje | winter | `Skylines/skopje-winter.png` | `07bf5fbf29ed792e452e212c3e33436ddedfbaeb4cb396d7af461e6ef588d401` |
| Sofia | summer | `Skylines/sofia-summer.png` | `3fbb0869b5fe36b0b73674ad2bc2e11caad23df6ec45fefac916b2cb00475418` |
| Sofia | winter | `Skylines/sofia-winter.png` | `6253352e51fd9cdd377783623c9fd8c5bfabd9bc8e6f817d546cf735f21f9798` |
| Stockholm | summer | `Skylines/stockholm-summer.png` | `8e8aad96db08e1fd2cc64e9a3327ab48cdc431f021e670831ba2538f958cb772` |
| Stockholm | winter | `Skylines/stockholm-winter.png` | `16494739c0b327db871ec9fb1954c1d52a34e3f73d1b4bdd9593d8140bdbdb1b` |
| Sucre | summer | `Skylines/sucre-summer.png` | `be2e5be5f0f93b639ad41cef28404146de09b5bee3e39b909c906bb8031c577d` |
| Sucre | winter | `Skylines/sucre-winter.png` | `8d082f2db74c11a8b90c43a1442d0e2e4174270aafce987432370378603a371b` |
| Sydney | summer | `Skylines/sydney-summer.png` | `f182cb171201a3635c9a67c890cbdd5e86582f8c8327bd7fd40db3763e4c6c66` |
| Sydney | winter | `Skylines/sydney-winter.png` | `40f029b0a47e7a7f2db45a2b2947c282b1d9384cb81aaf0d622a0916c128d6a9` |
| São Paulo | summer | `Skylines/sao-paulo-summer.png` | `0755a042a19b0a544a167b7e1a98d2571448167dd46cba70392a10d8d0b7d63d` |
| São Paulo | winter | `Skylines/sao-paulo-winter.png` | `6d01401cc9ad75ba4456da827bad5d4fcef1fa57a9308eca664047d4c7709f14` |
| Taipei | summer | `Skylines/taipei-summer.png` | `523a76779b9ad05d61aace2090bf7df2eec28e328ec55a5a0005737893f43afd` |
| Taipei | winter | `Skylines/taipei-winter.png` | `48bb356d74a95080619733e879c195bba2598629e49da9752aa31f12a5306f57` |
| Tallinn | summer | `Skylines/tallinn-summer.png` | `24eea47e3595a8e6545bdc44e129399c8d6c0a6f8b339392da6c77a59d1bbd3a` |
| Tallinn | winter | `Skylines/tallinn-winter.png` | `4d8770d4d6ec5312a4cd80c933310eaf4deb8e2ad4f88652b8d8ed54ed047829` |
| Tashkent | summer | `Skylines/tashkent-summer.png` | `79a851b8ae1b3a2ad91189c4b139ff094e3197eb027dc322ae2af02ce13a1083` |
| Tashkent | winter | `Skylines/tashkent-winter.png` | `d958ad2182ff4e98ef6e03683d14052c66755e4aa460d9736eaf8090e2c8684f` |
| Tbilisi | summer | `Skylines/tbilisi-summer.png` | `8c8c764b664273e71f8af686bd747be7151536f369c8a095086e5c5b7be4d1bb` |
| Tbilisi | winter | `Skylines/tbilisi-winter.png` | `4dcfc3af43117c255b4bde8ba4e5cf1642c2247bf2154d68e6fd92713469ff46` |
| Tehran | summer | `Skylines/tehran-summer.png` | `7b21444bbb60d2104e338aec0cbe73463d7ee3b69a57d2376a2422573cf294c2` |
| Tehran | winter | `Skylines/tehran-winter.png` | `38738b3da0a4a16ce0c8da7bc79b6353b603405c6a5061ac247763972d1e53ac` |
| Tirana | summer | `Skylines/tirana-summer.png` | `5e1cf0d33ca289490aad2cd6f62d6b536d8662953a6e93f98c922d6ad0f6f513` |
| Tirana | winter | `Skylines/tirana-winter.png` | `386882421c57b11f9c9097fa4e6a8fc4a458239de1e55f5e459478dba92bb25b` |
| Tokyo | summer | `Skylines/tokyo-summer.png` | `7613651900b229ad3abf0191f3717efd3e2dba854387e04a25950fc57fc09d1c` |
| Tokyo | winter | `Skylines/tokyo-winter.png` | `537294c6809c565325d58f99a986021606404c62b4f2c49ed3518d0855b157aa` |
| Toronto | summer | `Skylines/toronto-summer.png` | `a11c72f3e60013cb82aa97532b8d6892323929242a13d5e870fc08ed0893a6fe` |
| Toronto | winter | `Skylines/toronto-winter.png` | `736d0df8bbd08bd60e4dfdab997d54c88df908709a4919c140fb68778a2fbfdd` |
| Tripoli | summer | `Skylines/tripoli-summer.png` | `a9c34d2bbd7091a275834cbb6547bc8fce3dbcf16fd60c427f8a03245a157878` |
| Tripoli | winter | `Skylines/tripoli-winter.png` | `411a466cb4f1f50c1a9369b4807613b789665c62b0727b2b6f84e5e675e3e482` |
| Ufa | summer | `Skylines/ufa-summer.png` | `dae212382e5d529df727a6419e9b2edc2a9ba5476641bd6b84d3d6248403d359` |
| Ufa | winter | `Skylines/ufa-winter.png` | `069522fa47ef03ad3e7d51bdebdfbd8de748f866d026084f8468dab387bd8503` |
| Vaduz | summer | `Skylines/vaduz-summer.png` | `3bc90554279321b981f6d01afbdb6141c49425db63f44be9ae6a5c8c57f21f1c` |
| Vaduz | winter | `Skylines/vaduz-winter.png` | `07ee46bf7543ea2f32d820b3887beaff50c6471a2c2de5ddffa1ffada25770e1` |
| Valletta | summer | `Skylines/valletta-summer.png` | `951ac4952169b51c4dd536155d9e260c0bb69ffc88b7d8c3ce713c34eeac191f` |
| Valletta | winter | `Skylines/valletta-winter.png` | `de62952bcf7bccfb94b57102dfb5af098133e8f9972b81cb57b029f5ec330b9d` |
| Valparaíso | summer | `Skylines/valparaiso-summer.png` | `2aa88261fdce8afded2f68dcc3fc23b8501b93b803fd2d7891eb75afcf601ddc` |
| Valparaíso | winter | `Skylines/valparaiso-winter.png` | `334829b105b37581d46eba77545dac3a2ea021bf5b717d863037e4d7d820675f` |
| Vatican City | summer | `Skylines/vatican-city-summer.png` | `baedefc6d5b4f55ab74e7e24b5939695c3689acde0dc9f10600c3e3a63c117d0` |
| Vatican City | winter | `Skylines/vatican-city-winter.png` | `90ff4fc0d7cdfe0ef45d2ba006264abef96cc8efc0152c8815e576dff033ff31` |
| Vienna | summer | `Skylines/vienna-summer.png` | `fcc6f00c9cccca6883031ce6914f9ff537a599f6363a6e82103847b17146aada` |
| Vienna | winter | `Skylines/vienna-winter.png` | `410e60a35adbd1a2c65704b3a1a2e24cb1db37867446d6076ccf24abd76d4e83` |
| Vilnius | summer | `Skylines/vilnius-summer.png` | `562c6f06f94e6a43e915f754908d2bc1731cd1ecace884e46f9343feea978205` |
| Vilnius | winter | `Skylines/vilnius-winter.png` | `f25fb973b5edb476f87329d9c6811a0c9c192f5d34271bc841179643b8467065` |
| Warsaw | summer | `Skylines/warsaw-summer.png` | `59293d7a77ca9e5534005a27b8f8db4c50801cfe7d757c33d5548f2e39c064fe` |
| Warsaw | winter | `Skylines/warsaw-winter.png` | `575473d01c4d4231c7d39766074f0331c089409addd948c1ed33d35b37329b4e` |
| Yaoundé | summer | `Skylines/yaounde-summer.png` | `74bd6eb28ca787e5b5204a48758e666258597c14d254cdd3ef0215e1202f79a5` |
| Yaoundé | winter | `Skylines/yaounde-winter.png` | `59cc57d03af951699a5a366e866fd1cc7f5f967d09789309874c49d4540668b4` |
| Yekaterinburg | summer | `Skylines/yekaterinburg-summer.png` | `f0bb50f29740d6ffa63a08885b3b069f471ba807d19baa65d45b1358215e439c` |
| Yekaterinburg | winter | `Skylines/yekaterinburg-winter.png` | `af11d72288be5d82a96c1fc5cbebfaa96c810f7a3ca033811c36eba2b9ef98e9` |
| Yerevan | summer | `Skylines/yerevan-summer.png` | `becb939ca8fd1a5ad8d65589c6425660770145b48c4a10634a2c44be61ac9fca` |
| Yerevan | winter | `Skylines/yerevan-winter.png` | `23fe77ea056bf10909066d8bc9a10cb067fde5c0d2c10b80a02bdb3b020df959` |
| Zagreb | summer | `Skylines/zagreb-summer.png` | `c1264d1b6826fa0f81784acba05b1e3dfad1f709f79d766344fd73fe7189a7eb` |
| Zagreb | winter | `Skylines/zagreb-winter.png` | `fbdd1a6c22163ab4aae9b2dc0d399b22acd59fa32aea43ae27cc1b655306d556` |
