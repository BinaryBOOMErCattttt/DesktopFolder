# Changelog

All notable changes to this project are documented in this file.
本文件记录本项目的全部重要变更。

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v1.1.0] - 2026-08-17

### Added / 新增

- **内置应用图标**:硬编码生成的多尺寸图标(16 / 32 / 48 / 256 px),渐变圆角底 + 文件夹 + 彩色图标网格,嵌入 exe 资源
- **Embedded app icon**: hardcoded multi-size icon (16 / 32 / 48 / 256 px), gradient rounded background with a folder and colorful icon grid, embedded into the exe resources
- 窗口图标(Alt+Tab 缩略图)/ Window icon (Alt+Tab preview)
- 双语 README 与 MIT 协议 / Bilingual README and MIT license
- CHANGELOG.md(本文件 / this file)

## [v1.0.0] - 2026-08-17

### Added / 新增

- 桌面挂靠(WorkerW 桌面层)+ 毛玻璃质感(SetWindowCompositionAttribute)
- Desktop-attached (WorkerW layer) + frosted glass (`SetWindowCompositionAttribute`)
- A 折叠缩略图 / B 展开面板双态界面,图标自动铺满网格
- Two-state UI: collapsed thumbnail grid / expanded panel, icons auto-fill the grid
- 容量限制缩放:网格容纳全部图标后自动禁止继续拉伸
- Capacity-limited resizing: stretching is blocked once the grid fits all icons
- 多窗口持久化(config.json:位置 / 大小 / 网格模式 / 图标排列)
- Multi-window persistence (config.json: position / size / grid mode / icon layout)
- 图标拖拽排列、拖出移动/复制、.url / .website 快捷方式、隐藏扩展名
- Icon drag-arrange, drag-out move/copy, .url / .website shortcuts, hidden extensions
- 右键菜单:更换目录 / 刷新 / 新建 / 删除小组件 / 网格模式(2×/3×)/ 开机自启 / 退出
- Context menu: change folder / refresh / new / delete widget / grid mode (2×/3×) / auto-start / exit
