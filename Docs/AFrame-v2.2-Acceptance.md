# AFrame v2.2 首版验收

**本轮步骤 1–6 补齐目标已完成验收。**使用项目指定的 Unity 6000.6.9f1，从项目根目录执行命令。先保留当前改动，复用已有任务基线时无需重开任务。

| 项目 | 操作和通过标准 |
| --- | --- |
| TestGame 与测试入口 | 执行 `Tools/aframe test`，或用 `--feature character` 只跑角色用例。核对下一 Tick 执行、查询更新、事件和拒绝，以及运行顺序和依赖预校验。 |
| Cecil 与资源校验 | 执行 `Tools/aframe validate`。临时在隔离副本的玩法代码调用 `DateTime.Now`，再添加一个含 Bootstrap 的 Prefab，或把离线功能放到 Online Profile。检查对应规则拒绝并注明来源与范围。 |
| 生成与迁移 | 执行 `Tools/aframe import`。检查 Contracts 的 `DefinitionIds`、Composition 的 `link.xml` 和定义注册表。用 schema 0 的 `moveSpeed` 验证迁移；断开的跨定义引用或手改生成输出应触发错误。恢复后重新导入并更新 Context。 |
| Character 双视图 | 对两个场景分别执行 `Tools/aframe smoke --scene <场景路径>`。在 Player 内按 Tab 双向切换，确认键盘移动、角色显示和画面插值。 |
| 任务完整流程 | 在 Editor 关闭和打开时分别执行 `Tools/aframe check --task v22-first-release`。报告应记录编译、校验和两个 EditMode 选择器。临时越界改动应在进入 Unity 前被拒绝，已记录到基线的改动不应误判。 |

Unity 命令报告位于 `Logs/AFrame/`。新增公开 API、程序集或场景之后，用 `Tools/aframe context` 更新 Context；手工编辑生成文件会使校验失败。

旧的 macOS Player 验收记录覆盖能源回收站连续切场景与跨进程存档。本文新增验收仅覆盖 v2.2 首版，IL2CPP、物理和联机不在范围内。

本轮已核对 Unity 导入和编译、5 个 EditMode 用例、Character 2D 与 3D 场景的移动和坐标映射。也分别在 Editor 关闭和运行时完成了任务检查，按功能筛选 Character 用例通过。独立的范围检查已验证新增越界文件会被拦截，基线中原有改动不会误报。

隔离项目已确认 schema 0 的 Character 能迁移，跨文件引用能解析并生成 ID 常量。故意注入的禁用 API、非法场景／Prefab、离线功能与 Online Profile 的冲突、断开的定义引用及手改的 `link.xml` 均被检出。对应报告位于 `Logs/AFrame/v22-first-release/`。

macOS arm64 Mono Player 已通过 3D → 2D → 3D 往返检查。验收向游戏的 Unity Input System 发送 Tab 和 W+D 两组按键事件，核对了实际渲染、XY／XZ 映射和中间插值帧。日志与两种视图截图位于 `Logs/AFrame/v22-first-release/`。鼠标按钮可用于试玩，本轮以键盘入口验证切换。
