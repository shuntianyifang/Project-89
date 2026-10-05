# 历史地图候选参考

首版运行地图与部署预览为 `historical_grid_preview.png`；校准结果为 `calibration_results.json`。运行数据由 `tools/build_fulda_historical.py` 生成。预览包含人工粗化的道路、河流与边境，不能作为精确历史桥梁或图幅年代的核验证据。蓝点为北约棋子，红点为华约棋子，黑线为边境模型。

`tk200_candidate.png` 是德国 BKG TK200-DDR 的 `staat` 图层请求结果，配套 `.pgw` 为像素中心定位的 world file。EPSG:25832，50 米/像素，100×60 公里矩形；两者用于地图研究，尚未成为游戏地图。

来源：[BKG 产品说明](https://gdz.bkg.bund.de/index.php/default/webdienste/digitale-topographische-karten-dienste/wms-topographische-karte-1-200-000-ddr-wms-tk200-ddr.html)。署名：© GeoBasis-DE / BKG；许可：[数据许可德国—署名—2.0](https://www.govdata.de/dl-de/by-2-0)。请求与范围见 `map_source.json`。

已目视确认图中包含 Fulda、Bad Hersfeld、Geisa、Eisenach、Meiningen。Fulda 接近南边界，最终裁切仍需结合撤退通道检视；各图幅年代尚未逐幅核对。不能将扫描图中的全部符号直接解释为 1989-07-01 状态。

网格 x 向东、y 向南。单格对应 40×40 像素。地形、道路、河流、桥梁和边境仍需提取并核对；不得依据城市名字在图上的文字标签中心确定地理坐标。
