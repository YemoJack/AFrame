# AFrame 开发约束

- 领域规则放在功能的 Contracts/Core 程序集中，Core 不得引用 UnityEngine。
- 功能之间只能引用对方的 Contracts；Unity 场景对象只负责适配和表现。
- 命令必须入队，在固定 Tick 中执行；状态读取使用 Query，状态变化使用事件。
- 禁止可变静态状态、单例、GameObject.Find、Object.Find、SendMessage 和直接读取系统时间。
- Generated、Context 和 Unity Library 由工具维护。修改定义源、Contracts 或任务文件，不直接修改生成输出。
- 每个任务先读取相关 Context 和 Tasks 文件，修改范围必须保持在 allowedPaths 内。

## 常用命令

```text
Tools/aframe validate
Tools/aframe context
Tools/aframe smoke
Tools/aframe check --task <id>
```

提交前只需要确认项目能导入、编译、通过架构校验，并能运行 Smoke Check。
