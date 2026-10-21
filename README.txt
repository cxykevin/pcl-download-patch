PCL 下载页恢复补丁（pcl-download-patch）
==========================================

适用版本
--------
pcl.exe 2.99.0.0（32 位 .NET Framework 4.8）
已实测：补丁后 PCL 正常启动，IL 校验通过。

这包不含 PCL 本体
------------------
只有替换程序（bin\ManualPatcher.exe）、源码、依赖库和说明。
patcher 会读取你自己的 pcl.exe，生成打过补丁的副本，再替换回去。

用法
----
1. 完全退出 PCL。
2. 把整个 pcl-download-patch 文件夹放到 pcl.exe 所在目录。
3. 双击 apply.bat。
4. 启动 pcl.exe，点“下载”，应直接出现 MC 版本列表。

apply.bat 会：
- 把原 pcl.exe 备份为 pcl.exe.bak（只备份一次）；
- 调用 bin\ManualPatcher.exe 生成 pcl.exe.patched；
- 用 patched 覆盖 pcl.exe。

回滚
----
把 pcl.exe.bak 改回 pcl.exe 即可（或运行：
copy /y pcl.exe.bak pcl.exe）。

补丁改了什么（字节级 IL patch）
-------------------------------
1. PCL.PageDownloadInstall::LoaderInit
   把“关闭下载”假任务（ldstr + newobj LoaderTask）替换为
   ldsfld PCL.ModDownload::DlClientListLoader，其余指令用 nop 填满，
   方法总长度不变。
   等价于 PCLCE 里的：PageLoaderInit(..., DlClientListLoader, ...)

2. PCL.PageDownloadModify::LoadInstanceFinished
   把 CardMinecraft.Visibility 的 ldc.i4.2（Collapsed）改成
   ldc.i4.0（Visible）。

未恢复的功能
------------
- ModModpack.ModpackInstall（整合包安装）：原方法体只剩 Hint + ret，
  恢复需要新增 IL 并加长方法体，会移动后续方法、改异常表，
  等长字节 patch 做不到。
- 资源详情页的“安装整合包”按钮：同样需要新增 IL。
如需整合包安装，请用 PCL 社区版，或手动解压整合包到
.minecraft\versions。

目录说明
--------
bin\ManualPatcher.exe   替换程序（读取 pcl.exe，写 pcl.exe.patched）
bin\VerifyIL.exe        IL 校验工具（只读，不改文件）
bin\Mono.Cecil.dll      ManualPatcher/VerifyIL 的依赖
src\ManualPatcher.cs    字节级 patcher 源码（当前使用）
src\VerifyIL.cs         IL 校验源码
src\StaticPatcher.cs    Cecil 写回方案（写回会崩溃，仅作参考）
src\DnlibPatcher.cs     dnlib 写回方案（写回会崩溃，仅作参考）
docs\analysis.md        IL 偏移/Token/RVA 分析摘要
docs\old-dll-approach\  早期的 AppDomainManager + Harmony 注入方案
                        （会让 PCL 2.99 启动失败，仅作记录）

重新编译
--------
需要 .NET Framework 4.x 自带的 csc.exe（Windows 自带）。
双击 build.bat 即可重新生成 bin\ManualPatcher.exe 和 bin\VerifyIL.exe。

校验
----
bin\VerifyIL.exe pcl.exe
会打印 LoaderInit 的完整 IL 和 CardMinecraft 的可见性指令。

原理与限制
----------
- 只做等长字节替换：方法头 CodeSize、后续方法偏移、异常表都不变。
- 不修改 CLR 头、资源段、入口点。
- 依赖 PCL 2.99.0.0 的元数据布局；其他版本偏移不同，
  ManualPatcher 会按 Cecil 读到的 RVA/IL 偏移重新计算，
  但目标方法不存在或签名变化时会报错。
