# XrayUI Protobuf 编译修复

## 问题描述

编译 XrayUI-dev 项目时出现 3 个错误：

```
error CS0246: 未能找到类型或命名空间名"Xray"
error CS0246: 未能找到类型或命名空间名"StatsService"
error MSB3073: XamlCompiler.exe 命令已退出，代码为 1
```

**根本原因**：`Services/Traffic/stats_service.proto` 文件中的 Protocol Buffer 代码没有被正确生成。

## 修复方案

### 已应用的更改

1. **生成 Protocol Buffer 代码**
   - 使用 protoc 和 gRPC C# 插件手动生成了两个文件：
     - `Services/Traffic/StatsService.cs` - Protocol Buffer 消息定义
     - `Services/Traffic/StatsServiceGrpc.cs` - gRPC 服务客户端和服务定义

2. **更新项目配置**
   - 从 `XrayUI-dev.csproj` 中移除了 `<Protobuf>` 构建项
   - 生成的 `.cs` 文件现在作为常规源代码文件编译

### 生成的文件包含

✅ `namespace Xray.App.Stats.Command`
✅ `StatsService.StatsServiceClient` 类  
✅ Protocol Buffer 消息类：`QueryStatsRequest`, `Stat`, `QueryStatsResponse`

## 在 Windows 上重新生成（如需）

如果需要在 Windows 上重新生成这些文件：

```bash
# 1. 安装 protoc（如果尚未安装）
choco install protoc  # 或使用其他包管理工具

# 2. 使用 Grpc.Tools 中的插件生成代码
cd Services\Traffic

protoc --csharp_out=. --grpc_out=. ^
  --plugin=protoc-gen-grpc="%USERPROFILE%\.nuget\packages\grpc.tools\2.71.0\tools\windows_x64\grpc_csharp_plugin.exe" ^
  stats_service.proto
```

## 验证编译

```bash
# 在 Linux 上使用 Windows 目标框架编译
dotnet build -p:EnableWindowsTargeting=true

# 在 Windows 上编译（正常命令）
dotnet build
```

## 故障排除

### 如果仍然出现 CS0246 错误

确保：
1. `Services/Traffic/StatsService.cs` 存在且包含正确的命名空间
2. `Services/Traffic/StatsServiceGrpc.cs` 存在且包含 `StatsServiceClient` 类
3. 项目文件中没有 `<Protobuf>` 项（已移除）

### 如果仍然出现 XAML 编译器错误（仅限 Linux）

这是预期行为，因为 XAML 编译器是仅限 Windows 的工具。在 Windows 上编译项目可正常工作。

## 技术细节

- **Proto 定义文件**：`Services/Traffic/stats_service.proto`
- **Generated 命名空间**：`Xray.App.Stats.Command`
- **主要客户端类**：`Xray.App.Stats.Command.StatsService.StatsServiceClient`
- **使用场景**：`Services/Traffic/XrayStatsClient.cs` 中用于通过 gRPC 查询 Xray 统计信息
