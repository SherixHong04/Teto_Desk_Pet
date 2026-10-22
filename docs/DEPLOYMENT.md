# 部署与维护

支持Windows 10/11 x64和.NET Framework 4.8。支持三种配置：ChatGPT/Codex CLI、DeepSeek Harness、两者同时安装；对话客户端是必要条件。

检测只检查环境变量、当前用户的常见安装目录、PATH与Windows安装注册信息，不读取凭证，不执行探测出来的任意命令，也不扫描全盘。完整Harness安装必须在EXE旁包含resources/app.asar。匹配不到的便携位置由用户选择。

设置文件位于用户Data/providers.json，只有所选服务及可选客户端路径，不包含密钥。环境变量TETO_CODEX_EXE、TETO_HARNESS_EXE可指定路径；手动设置优先，其后尝试环境和常见目录。旧路径无效时继续检测其他有效安装。会话分别存为session.json和session-deepseek.json，切换不会混用原生会话ID。

默认状态根目录是%LOCALAPPDATA%\TetoDesk；其中Data保存设置、会话和附件，Tasks为默认工作目录。portable.flag切换为EXE所在目录；TETO_DESK_HOME可为开发/受控部署指定独立根目录。不要把运行后的Data/Tasks加入Git或重新压入分发包。账户凭证始终由原生客户端管理。

发布物仅从源码构建输出和固定说明文件创建。程序包包含EXE/config、两个NAudio DLL、Bridge连接文件/许可证及使用/许可说明；源码包包含源码、工具、文档、对应工程和必要资源。检查器拒绝Data/Tasks、会话设置、密钥、日志、测试产物、个人机器路径和符号链接。`dist/`、`.local/`与构建/测试输出均被Git忽略。

CI不登录账户、不启动模型任务、不注入输入或显示窗口。部署矩阵验证单通道菜单、双通道切换、余额/额度对应关系、路径变更、过期偏好、忙时保护和会话保持。真实客户端/账户兼容性只能在具备相应安装的机器另行验证；不把模拟安装测试宣称为跨机器人工验收。

官方参考：

- Codex CLI：https://learn.chatgpt.com/docs/cli
- ChatGPT登录：https://learn.chatgpt.com/docs/auth
- DeepSeek官方桌面客户端：https://github.com/deepseek-ai/deepseek-harness/tree/master/apps/desktop
- 角色使用：https://kasaneteto.jp/guidelines/character.html
