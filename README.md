<h1 align="center">🚀 XrayUI-Portable</h1>

<p align="center">
  <b>为 Windows 打造的极简、轻快、全绿色免安装的科学冲浪客户端。</b><br/>
优势：1.解压即用，免安装，可迁移；2.可同时连接多个服务器节点，通过不同端口分流同时访问；3. 纯净无污染，零环境变量侵入；4. 软件速度极快，系统资源占用极低；5. 端口自动设置，避免冲突；6. 核心异常自愈与 AI 长会话稳定保活。
</p>

<p align="center">
  <a href="https://github.com/vimbackground/XrayUI/releases/latest">
    <img src="https://img.shields.io/github/v/release/vimbackground/XrayUI?color=blue&label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC" alt="Release">
  </a>
  <img src="https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/Portable-%E7%BB%BF%E8%89%B2%E4%BE%BF%E6%90%BA-brightgreen" alt="Portable">
  <img src="https://img.shields.io/badge/.NET_10-Native_AOT-purple" alt="Native AOT">
  <a href="https://linux.do">
    <img src="https://img.shields.io/badge/LinuxDo-Community-1f6feb" alt="LinuxDo">
  </a>
</p>

---

## 🌟 为什么选择 XrayUI-Portable？

市面上的代理工具很多，但往往存在“换电脑配置全丢”、“一次只能连一个国家节点”、“内存占用大”、“界面不够美观”等痛点。**XrayUI-Portable** 正是为此而生：

### 1. 🎒 真正的全绿色免安装（配置永不丢失）
- 所有订阅、节点列表、自定义规则及日志，**全部存放在软件同级的 `Data/` 文件夹中**；
- 换电脑、重装系统或放在 U 盘里？**整包复制过去直接双击就能用**，所有节点和设置完好如初，再也不用繁琐重配！

### 2. 🔀 独创：多节点同时上网（多节点分流模式）
- **告别来回切换连接**：以往看剧、游戏、查资料如果在不同地区，必须反复断开重连；
- **一台电脑同时连多个节点**：主节点连香港用于网页查资料，同时可为日本节点分配独立端口（如 `:10809`）看流媒体，为新加坡节点分配 `:10810` 用于游戏。第三方软件只需指定对应端口，即可实现定向分流！
- **渐进式极简设计**：平时不开启该模式时，界面与普通客户端一样纯净简洁，没有任何复杂选项打扰。

### 3. 🛡️ 纯净系统代理 & 零环境变量污染（开发者友好）
- **不篡改环境变量**：彻底不再向 Windows 用户或系统环境变量中注入或删除 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY`、`NO_PROXY`，彻底杜绝导致 Git、Python、Docker、curl 以及 VS Code / Codex 终端网络异常或死锁的问题；
- **智能代理所有权 (Proxy Ownership)**：精细化接管系统代理，退出或停用时仅还原由本软件自身配置的系统代理，绝不误清或覆盖第三方代理工具的状态；
- **TUN 虚拟网卡精准回收**：重构网卡清理机制，退出时严格仅清理当前专用的虚拟网卡路由，杜绝误动物理网络。

### 4. ⚡ 最新官方 Xray 内核 & AI 长会话连接保障
- 内置升级至最新的 **Xray-core 26.9.30** 官方候选核心，全面支持主流协议及最新的 **HTTPUpgrade** 现代传输协议；
- 深度优化长会话连接参数（15秒 TCP Keepalive、300秒半关闭超时缓冲），保障 AI 编程助手（Codex, Copilot, Claude 等）长推理与流式响应连接稳定不断线。

### 5. 🌐 全能协议支持与智能分流
- 全面支持主流协议：**VLESS, VMess, Trojan, Shadowsocks, Hysteria2, WireGuard** 及现代 **HTTPUpgrade** 传输协议与 **链式代理 (Proxy Chain)**；
- 内置 **智能分流模式**（国内网站直连不绕路，国外网站高速代理），并支持 **TUN 虚拟网卡模式** 与 **AI 常用服务解锁检测**。

### 6. 🖱️ 双击极速操作 & 智能防冲突与自愈
- **双击即切**：双击已连接的节点立即停止；双击未连接的节点秒连并自动断开其他节点；
- **智能避开端口占用**：首次启动或日常运行中，如果遇到端口被其他软件占用，系统会自动预检并一键分配空闲端口，小白用户也能无感上手；
- **核心异常自愈**：内核如遇外部异常退出，UI 自动检测并重置状态，杜绝界面假死。

### 7. 🎨 原生 Windows 现代视觉（秒开、极省内存）
- 采用微软最新的 **WinUI 3** 原生界面，完美支持 Windows 11 / 10 风格；
- 支持 **深色 / 浅色模式** 与 **云母 (Mica) / 亚克力 (Acrylic) 毛玻璃半透明** 材质；
- 采用 **Native AOT** 机器码原生编译，软件体积仅约 16MB，双击瞬间秒开，内存占用极低。

### 8. ⌨️ 超实用的全局快捷键（老板键）
- **一键隐藏/唤出窗口**（老板键，下班或摸鱼必备）；
- **一键开关代理** / **一键切换分流与全局模式**；
- 允许自由自定义快捷键组合。

---

## 📥 下载与安装

前往 [👉 Releases 页面](https://github.com/vimbackground/XrayUI/releases/latest) 下载最新版本：

| 版本类型 | 适用人群 | 下载链接 |
| :--- | :--- | :--- |
| **x64 常规便携版 (强烈推荐 🌟)** | 绝大多数 Windows 电脑（Intel / AMD 64位处理器） | [下载 XrayUI-win-x64.zip](https://github.com/vimbackground/XrayUI/releases/latest) |
| **x64 独立完整版 (wasdk)** | 未安装 Windows App SDK 运行时或启动提示缺少运行库的用户 | [下载 XrayUI-win-x64-wasdk.zip](https://github.com/vimbackground/XrayUI/releases/latest) |

> 💡 **使用方法**：下载 `.zip` 压缩包后，**解压到任意文件夹**，双击运行 `XrayUI-Portable.exe` 即可使用！  
> 📌 *注：GitHub Actions 流水线专注于交付 x64 版本。ARM64 用户可使用源码通过 `dotnet publish` 进行本地构建。*

---

## 🧭 常见问题 (FAQ)

<details>
<summary><b>代理环境变量与 VS Code / Codex 连接异常如何处理？</b></summary>

XrayUI 不再自动设置或删除 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY`、`NO_PROXY`，Windows 系统代理功能保持可用。此前版本或开发版已经写入的环境变量不会被自动清理。

1. 完全退出旧版 XrayUI，再打开 Windows 搜索中的“编辑账户的环境变量”，检查“用户变量”中的上述四项，以免旧版退出逻辑继续修改变量。
2. 确认某个值确实由 XrayUI 写入后，再手动删除该项。历史值可能包括 `HTTP_PROXY=http://127.0.0.1:9999`、`HTTPS_PROXY` / `ALL_PROXY=socks5h://127.0.0.1:9999` 和 `NO_PROXY=localhost,127.0.0.1,::1`，实际端口以当时配置为准。仅值相同不能证明其来源，自己配置或其他软件需要的值应保留。
3. 清理完成后完全退出所有 VS Code 窗口及后台进程，并重新启动 VS Code；必要时重新打开终端或注销登录，使新进程获得更新后的环境。

如果仍有反复重连或长会话挂起，请保留故障时间附近的代理与扩展日志。取消环境变量写入不能证明所有挂起问题已经解决。

</details>

<details>
<summary><b>Q1: 怎么开启“多节点分流（多个节点同时连接）”？</b></summary>

1. 点击右下角 **【软件设置】（齿轮图标）**；
2. 找到 **“多节点分流模式”** 并打开开关，点击保存；
3. 返回主界面，在需要作为独立端口的节点上 **右键 -> 设置独立分流端口**（或开启独立端口）；
4. 此时该节点会显示 `[🎧 专口 :端口号]`，在其他软件中配置该端口作为代理即可同时上网！
</details>

<details>
<summary><b>Q2: 怎么将所有配置迁移到新电脑？</b></summary>

直接将包含 `XrayUI-Portable.exe` 和 `Data/` 文件夹的整个目录复制到新电脑上，打开即可继续使用，无需重新导入订阅。
</details>

<details>
<summary><b>Q3: 遇到端口冲突无法启动代理怎么办？</b></summary>

软件内置了冲突检测，如果当前端口被其他软件占用，会自动弹出提示并推荐一个空闲端口，点击确认即可一键切换并启动。
</details>

---

## 🛠️ 开发者指南 (编译源码)

如果你希望自行编译本项目：

```powershell
# 使用 .NET 10 SDK 进行 Native AOT 编译
dotnet publish XrayUI-dev.csproj -c Release -r win-x64 -p:Platform=x64 -p:SelfContained=true -p:PublishAot=true -p:PublishTrimmed=true -p:WindowsAppSDKSelfContained=false -p:BuildingForCI=true
```

---

## 🙏 致谢 (Acknowledgements)

- 原项目：[PhoenixNil/XrayUI-dev](https://github.com/PhoenixNil/XrayUI-dev)（感谢原作者构建的优秀 WinUI 3 客户端框架）
- 核心引擎：[Xray-core](https://github.com/XTLS/Xray-core)
- 驱动支持：[Wintun](https://www.wintun.net/)
- 社区交流：[Linux.do 社区](https://linux.do)

---

## 📄 开源协议

本项目基于 [Apache License 2.0](LICENSE) 开源协议。
