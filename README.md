# Jime

一个驻留托盘的 Windows 日语输入小工具，不安装或启用系统输入法。输入罗马字后使用随附的 Mozc 引擎生成假名、汉字和候选，最后把结果注入当前窗口。

## 使用

```bash
dotnet run
```

在任意文本框中按 `Ctrl+Alt+J` 呼出输入窗口。窗口会记住上一次拖动后的位置。

### 输入与候选

- 输入罗马字，未确认内容带下划线；按住 Shift 输入大写字母
- `Space`：确认当前候选
- `1`–`9`：选择当前页候选
- `-` / `=`：上一页 / 下一页候选；`PgUp` / `PgDn` 同样可用
- 首页按 `-`：输入原始 `-`，转换成长音 `ー`
- 末页按 `=`：不执行操作
- `Alt+Enter`：把未确认内容确认成平假名
- `Ctrl+Enter`：把未确认内容确认成片假名
- 候选存在时按 `Enter`：保留未确认的英文输入
- 无候选时按 `Enter`：把已确认内容注入目标窗口
- `Esc`：取消并关闭

### 编辑

- `←` / `→`、`Home` / `End` 移动光标
- `Backspace` / `Delete` 删除字符
- 无未确认输入时，可在已确认正文中间定位并插入新内容
- 存在未确认输入时，光标限制在未确认部分内；退格退空后回到已确认正文状态
- `Shift + ←/→` 只移动光标，不建立选区
- 鼠标点击输入内容不会定位光标
- `Ctrl+A/C/X/V/Z/Y` 不执行编辑操作
- `Alt + -/=`：输入原始 `-` / `=`
- `Alt+Shift + -/=`：输入 `_` / `+`

## 构建

要求：

- Windows 10/11 x64
- .NET 9 SDK
- Visual C++ runtime，或使用仓库内随附的最小运行库

```bash
dotnet build
dotnet run
```

发布单文件：

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Mozc 转换引擎

仓库包含运行所必需的 `mozc_server.exe` 和 MSVC 运行库，不包含 Mozc 源码、Qt UI、安装包或构建产物。

独立检查转换引擎：

```bash
dotnet run --project Jime.Cli -- ochitsuiteiru
```

输出示例：

```text
ochitsuiteiru:
1. 落ち着いている
...
```

## 项目结构

- `MainWindow.xaml.cs`：窗口、输入状态机、候选与光标
- `MozcConverter.cs`：Mozc named-pipe IPC
- `RomajiConverter.cs`：罗马字到假名
- `TextInjector.cs`：剪贴板注入
- `PositionSettings.cs`：窗口位置持久化
- `Jime.Cli/`：转换引擎命令行测试工具
- `Protos/`：Mozc protobuf 协议
- `third_party/runtime/`：最小 Mozc 运行引擎

## License

Jime 自身代码使用 [Unlicense](LICENSE)，放弃版权，进入公有领域。

随附的 Mozc 二进制约受 BSD 3-Clause License 约束；MSVC 运行库受 Microsoft 许可约束。详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
