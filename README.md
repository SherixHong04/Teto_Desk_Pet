# Teto Desk Pet

<img src="src/Assets/teto-icon.png" width="128" alt="Teto Desk头像">

一个Windows桌宠：让Teto陪你工作，并通过本机的 **ChatGPT / Codex CLI** 或 **DeepSeek Harness** 处理对话、图片与文件任务。使用已有客户端的账户登录，不要求填写API Key。

[English](README.en.md) · [下载当前版本](https://github.com/SherixHong04/Teto_Desk_Pet/releases/latest) · [使用说明](docs/QUICKSTART.zh-CN.md)

## 安装与开始使用

1. 准备至少一个支持的客户端，并在该客户端完成登录。
   - **ChatGPT通道**：需要可运行的原生Codex CLI，执行 `codex login` 并用ChatGPT账户登录。只有ChatGPT网页/桌面界面，而没有可用Codex CLI，不等同于已配置该通道。安装方式以[官方CLI文档](https://learn.chatgpt.com/docs/cli)和[登录文档](https://learn.chatgpt.com/docs/auth)为准。
   - **DeepSeek通道**：安装[官方DeepSeek Harness Desktop](https://github.com/deepseek-ai/deepseek-harness/tree/master/apps/desktop)，保留完整安装的`resources`目录并登录DeepSeek账户。当前适配官方Electron载体的原生CLI入口；其他社区壳不保证兼容。
2. 从Releases下载 `TetoDesk-0.1.21-windows-x64.zip`，完整解压后运行 `TetoDesk.exe`。需要Windows 10/11 x64和.NET Framework 4.8，不需要安装Python或开发工具。
3. 启动只显示桌宠。点击她下方的气泡、托盘中的打开入口或按 **Ctrl+Alt+T** 打开聊天框。

便携包没有数字签名。程序不会为你安装、登录或升级其他客户端，也不会读取或复制它们的账户密钥。

## 根据本机客户端调整

| 本机可用客户端 | 服务菜单 | 桌宠右键/托盘中的查询 |
|---|---|---|
| 只有Codex CLI / ChatGPT登录 | 只显示ChatGPT | 5小时与每周剩余额度、读条、重置倒计时 |
| 只有DeepSeek Harness | 只显示DeepSeek Harness | 账户余额、充值/赠送明细和峰谷时段 |
| 两者都有 | 均可选择，保留各自会话 | 跟随当前所选服务自动切换 |

支持环境变量、PATH、常见npm/客户端安装目录和Windows注册信息。便携客户端放在自定义位置时，可在首次定位提示或服务菜单的 **定位已安装的客户端** 中选择EXE；新增或移除客户端后使用 **重新检测本机客户端**。不会扫描整块硬盘，也不会把未发现的服务列成可选CLI。首次两者皆有时优先DeepSeek，之后记住可用的上次选择；旧选择在新机器不存在时改用已安装的另一个。

客户端存在不等于已登录或账户有额度。模型、强度和图片能力均以当前原生客户端返回为准；查询额度不会启动模型任务。本项目不提供没有对话客户端的独立运行配置。

## 交互与文件

- 拖动桌宠、悬停跳跃一次、空闲随机吃法棍，双击可触发微怒/眩晕彩蛋；查询采用手绘气泡与淡入淡出。
- 聊天框可缩放、添加附件、切换模型和思考强度，支持原生工具执行、权限确认和停止任务。
- 任务目录由用户选择。文字/Office文档可提取内容，图片取决于模型能力；PDF文本提取可选依赖本机Python+pypdf，否则交给客户端文件工具处理。
- DeepSeek语音附件先由本地官方SenseVoice插件转写，再交给模型。首用会下载识别资源；它不是模型直接识别任意声音。每条至多10个文件，每个20MiB，总计100MiB。

## 数据与隐私

默认数据位于 `%LOCALAPPDATA%\TetoDesk\Data`，默认任务目录位于 `%LOCALAPPDATA%\TetoDesk\Tasks`。应用可放在只读安装目录，更新EXE不会覆盖用户数据。需要完全便携时，在EXE同目录创建空白 `portable.flag`，数据会改存该目录；请确保该位置可写。

仓库与发布ZIP不包含开发者的聊天、附件、余额、客户端绝对路径、账户登录、任务文件或测试日志。`.local`仅为开发机的忽略目录，严禁上传。详情见[数据与打包](docs/DEPLOYMENT.md)。

## 从源码编译

Windows自带的.NET Framework C#编译器即可构建，所需NAudio依赖和许可证已包含：

```powershell
.\src\Build.ps1
.\src\Test.ps1 -Mode deployment
.\tools\Package.ps1
```

运行包、包含工程的源码包和SHA-256校验文件输出到`dist/`。打包采用允许清单并检查压缩包内容，不复制任何Data/Tasks目录。GitHub Actions在Windows上运行离屏测试和构建；main首次提交新版本时自动生成对应标签和Releases附件，已有版本不会被覆盖，也支持手动推送版本标签。鼠标交互仍需人工验收。

## 许可

代码与文档采用[MIT](LICENSE)。角色图集、图标和Aseprite工程见[素材说明](ARTWORK.md)；依赖见[第三方声明](THIRD_PARTY_NOTICES.md)。这是非官方同人项目，与角色官方、OpenAI、DeepSeek没有隶属关系。
