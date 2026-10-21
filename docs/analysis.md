# PCL 2.99.0.0 下载页静态 Patch 分析

## 目标文件

- `pcl.exe` 5,980,160 字节
- SHA-256 `6B6E1260CB5A1D6A303855F1C60CAB3D0BC1AEBBA7E40A964180188B271E7386`
- 32 位 PE32，.NET Framework 4.8，CLR header v4.0.30319
- 镜像基址 0x400000，.text RVA 0x2000 / 文件 0x200

## 关键元数据

- `PCL.ModSecret::RestrictMcDownload`
  - token `0x040001AD`，`Public Static Literal`，常量 `True`
  - `FieldRVA = 0`（literal 无存储槽），全程序集无 `ldsfld`
- `PCL.ModDownload::DlClientListLoader`
  - token `0x0400040F`

## Patch 1：PageDownloadInstall::LoaderInit

- 方法体 RVA `0x2DEB4`，文件偏移 `0x2C0B4`
- fat header 12 字节，IL 起始文件偏移 `0x2C0C0`
- 原 IL：
  - `IL_001A ldstr "关闭下载"`
  - `IL_001F ldsfld $IR3-3` / `brfalse` / `newobj Action<...>`
  - `IL_0043 ldnull` / `IL_0044 ldc.i4.2` / `IL_0045 newobj LoaderTask<...>`
- Patch：
  - 在 `IL_001A` 写入 `7E 0F 04 00 04`（`ldsfld DlClientListLoader`）
  - `IL_001F` 到 `IL_0049` 全部填 `0x00`（`nop`）
  - 保留 `IL_004A` 起的 `ldarg.0 / ldftn _Lambda$__R3-4 / newobj Action<...> / ldnull / ldc.i4.1 / call PageLoaderInit / ret`
- 方法头 `CodeSize` 不变，方法体长度不变，后续方法偏移不变

## Patch 2：PageDownloadModify::LoadInstanceFinished

- `IL_012E ldc.i4.2`（`Collapsed`）文件偏移 `0x322E6`
- Patch：字节 `0x18` → `0x16`（`ldc.i4.0` = `Visible`）

## 校验方法

```
bin\VerifyIL.exe pcl.exe
```

预期输出：

```
IL_001A ldsfld ... PCL.ModDownload::DlClientListLoader
IL_001F..IL_0049 nop
IL_004A ldarg.0
...
IL_012E ldc.i4.0
```

## 失败记录（不要重复）

1. **AppDomainManager + Harmony 运行时注入**
   - 补丁能应用，但 PCL 启动失败：exit 255，`PCL/Log1.txt` 不写，窗口不出现
   - 根因：Harmony 在 `AssemblyLoad` 回调中抢先 `Assembly.Load` `MeloongCore`，
     破坏 PCL 自己的 `Application.New` + `AssemblyResolve` 流程
   - 同步、后台、延迟 8/15/20 秒、移除轮询、移除事件订阅均无法稳定
2. **Cecil `asm.Write` / dnlib `mod.Write`**
   - 读取和 IL 修改都成功，写回 `pcl.exe` 时进程崩溃（0 字节输出，
     `Exception.ToString()` 都失败）
   - 推测与 204KB 资源段 + 大元数据的 PE writer 有关
3. **手工字节 patch（当前方案）**
   - 只做等长替换，绕开 PE writer，成功

## 环境限制

- 只恢复 `LoaderInit` 与 `CardMinecraft`
- `ModModpack.ModpackInstall` 需要新增 IL/加长方法体，等长 patch 做不到
- PCLCE 社区版是完整的未禁用实现，可直接使用
