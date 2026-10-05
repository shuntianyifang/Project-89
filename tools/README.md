# 工具脚本目录

此目录用于存放今后仍需反复使用的辅助脚本。

## 构建与自动化测试

在项目根目录执行 `powershell -ExecutionPolicy Bypass -File tools/validate.ps1`。
脚本先构建，再启动 Godot .NET 无界面测试；日志保存在系统临时目录。
除了检查退出码，还检查失败日志和测试结束标记，避免部分测试集失败但总运行器返回成功。

Godot 可通过 `-GodotPath '完整的 Godot .NET exe 路径'` 或环境变量 `GODOT_EXE` 指定。
未指定时使用 PATH 中的 `godot4`/`godot`，或自动发现 D 盘唯一的 `Godot*_mono_win64` 控制台程序。
当前本机安装位于 `D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`。

Codex 沙箱若无法读取用户 NuGet 配置，需要在获准的沙箱外环境执行此脚本。
无需修改用户配置的访问权限或降低项目 SDK 版本。

- 只在一次性迁移/修补中使用的脚本请归档到 archive/legacy_scripts
- 未来需要反复执行的脚本请放在这里
- 不再使用的脚本应直接删除
