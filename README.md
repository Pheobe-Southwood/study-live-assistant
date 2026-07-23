# 自习直播助手

[![CI](https://github.com/Pheobe-Southwood/study-live-assistant/actions/workflows/ci.yml/badge.svg)](https://github.com/Pheobe-Southwood/study-live-assistant/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Pheobe-Southwood/study-live-assistant)](https://github.com/Pheobe-Southwood/study-live-assistant/releases/latest)
[![License: GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)

“自习直播助手”是一款面向 Windows 自习直播的本地工具。它把今日任务、实际学习时长和倒数日整理成可供 OBS 捕获的信息画布，同时提供一个尽量克制的学习控制窗口。

## 主要功能

- 固定尺寸、无边框的直播窗口，包含顶部信息栏、左侧任务清单和纯色绿幕区。
- 时间型任务自动推进；页数、章节、题目等计数任务手动推进，同时始终记录实际用时。
- 目标开始时间用于计划排序，第一次播放时自动记录实际开始时间。
- 按日期建立任务、按起始时间排序、一键复制前一天计划。
- 基础专注、软萌、游戏风三套任务卡主题与可配置进度条。
- 系统全局快捷键、倒数日、今日/近 7 天/近 30 天基础统计。
- SQLite 本地持久化，不要求登录，不上传任务和学习记录。

## 下载与运行

1. 在 [Releases](https://github.com/Pheobe-Southwood/study-live-assistant/releases/latest) 下载 `StudyLiveAssistant-*-win-x64.zip`。
2. 可用同名 `.sha256` 文件核对下载包完整性。
3. 解压后双击 `StudyLiveAssistant.exe`。

程序目前没有商业代码签名证书，首次运行时 Windows SmartScreen 可能显示提醒。请只从本仓库 Releases 下载并核对校验值。

详细配置、OBS 捕获和快捷键说明见 [使用说明](docs/USAGE.md)，数据处理方式见 [隐私说明](PRIVACY.md)。

## 数据位置

用户数据保存在 `%LocalAppData%\StudyLiveAssistant`：

- `data.db`：任务、设置、会话和统计数据；
- `Assets`：导入的信息区背景图副本；
- `Logs`：发生异常时写入的本地日志。

删除程序 EXE 不会自动删除这些数据。

## 开发与验证

项目使用 WPF、.NET 10 和 SQLite。仓库的 `CI` 工作流在 `windows-latest` 上执行还原、测试和 Release 构建；`v*` 标签由 `Release` 工作流生成 Windows x64 自包含单文件便携版。

本项目约定不在当前维护工作区执行本地编译或测试，验证结果以 GitHub Actions 为准。

## 许可证

本项目以 [GNU General Public License v3.0](LICENSE) 发布。
