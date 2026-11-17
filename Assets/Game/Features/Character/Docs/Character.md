# Character

`Contracts` 定义输入、命令、位置查询和文本配置类型。输入适配器在帧中采样方向与冲刺状态，运行时在每个 Tick 开始时冻结采样，`Core` 根据该值更新位置。视图在 Tick 结束后读取状态并在帧中插值。

默认速度来自 `Definitions/character.default.aframe.json`。修改后用 **AFrame → Content → Import Definitions** 重新导入。Composition 从注册表构建 App 定义服务，并在会话中注入角色实现。
