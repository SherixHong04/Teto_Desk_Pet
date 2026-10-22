# 第一次使用

1. 安装并登录Codex CLI（使用ChatGPT账户）或官方DeepSeek Harness Desktop，至少一个。
2. 完整解压Windows x64 ZIP，不要单独拷贝EXE；保留Bridge、两个NAudio DLL及配置文件。
3. 双击TetoDesk.exe，只显示桌宠。点击下方气泡或Ctrl+Alt+T打开对话。
4. 程序只列出发现的客户端。自定义安装位置可手动定位EXE，需保留Harness的resources目录。只有ChatGPT桌面/网页界面时，请依照 https://learn.chatgpt.com/docs/cli 安装或定位原生Codex CLI，再用`codex login`完成ChatGPT登录。
5. 服务菜单可以重新检测客户端。任务文件夹决定模型的工作位置；更改目录会开始新会话，原文件不会删除。
6. ChatGPT查询5小时/每周额度；DeepSeek查询账户余额与峰谷期。未登录、网络错误或服务不支持查询时明确显示错误，不拿另一个服务的数据代替。

默认数据在%LOCALAPPDATA%\TetoDesk，两个服务分别保存会话。发布包没有预设账户、历史、模型、工作目录或额度。切换模型和强度以实时客户端目录为准，安装了客户端并不保证账号可使用全部模型。

首次语音转写需要联网准备官方本地识别资源。图片识别、文件工具和模型可用性取决于已登录的客户端；不支持的功能会提示，不能以更改显示名称获得能力。

托盘退出会在有任务时询问；程序不会强制结束你另行打开的Harness/Codex窗口。程序未签名，运行前请核对Releases中的SHA256SUMS.txt。
