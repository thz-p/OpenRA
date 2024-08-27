#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using OpenRA.Network;

namespace OpenRA.Server
{
	sealed class Program
	{
		static void Main(string[] args)
		{
			try
			{
				Run(args);
			}
			catch
			{
				// Flush logs before rethrowing, i.e. allowing the exception to go unhandled.
				// try-finally won't work - an unhandled exception kills our process without running the finally block!
				Log.Dispose();
				throw;
			}
			finally
			{
				Log.Dispose();
			}
		}

		static void Run(string[] args)
		{
			// 解析传入的命令行参数
			var arguments = new Arguments(args);

			// 从参数中获取 Engine.EngineDir 并覆盖默认的引擎目录
			var engineDirArg = arguments.GetValue("Engine.EngineDir", null);
			if (!string.IsNullOrEmpty(engineDirArg))
				Platform.OverrideEngineDir(engineDirArg);

			// 从参数中获取 Engine.SupportDir 并覆盖默认的支持文件目录
			var supportDirArg = arguments.GetValue("Engine.SupportDir", null);
			if (!string.IsNullOrEmpty(supportDirArg))
				Platform.OverrideSupportDir(supportDirArg);

			// 添加不同类型的日志通道，分别记录调试、性能、服务器、NAT 和 GeoIP 信息
			Log.AddChannel("debug", "dedicated-debug.log", true);
			Log.AddChannel("perf", "dedicated-perf.log", true);
			Log.AddChannel("server", "dedicated-server.log", true);
			Log.AddChannel("nat", "dedicated-nat.log", true);
			Log.AddChannel("geoip", "dedicated-geoip.log", true);

			// 特殊处理 Game.Mod 参数：如果它是一个有效的文件系统路径，则覆盖 mod 搜索路径，并替换为 mod ID
			var modID = arguments.GetValue("Game.Mod", null);
			var explicitModPaths = Array.Empty<string>();
			if (modID != null && (File.Exists(modID) || Directory.Exists(modID)))
			{
				// 如果 modID 是路径，则将其添加到显式 mod 路径中，并提取路径的文件名作为 mod ID
				explicitModPaths = new[] { modID };
				modID = Path.GetFileNameWithoutExtension(modID);
			}

			// 如果 modID 为空，则抛出异常，表示缺少或未找到 Game.Mod 参数
			if (modID == null)
				throw new InvalidOperationException("Game.Mod argument missing or mod could not be found.");

			// HACK: 确保引擎代码假设的 Game.Settings 被设置
			Game.InitializeSettings(arguments);
			var settings = Game.Settings.Server;

			// 初始化 NAT 网络穿透模块
			Nat.Initialize();

			// 从环境变量中获取 mod 搜索路径，如果不存在则使用默认路径
			var envModSearchPaths = Environment.GetEnvironmentVariable("MOD_SEARCH_PATHS");
			var modSearchPaths = !string.IsNullOrWhiteSpace(envModSearchPaths) ?
				FieldLoader.GetValue<string[]>("MOD_SEARCH_PATHS", envModSearchPaths) :
				new[] { Path.Combine(Platform.EngineDir, "mods") };

			// 创建一个 InstalledMods 实例，用于管理安装的 mod
			var mods = new InstalledMods(modSearchPaths, explicitModPaths);

			// 输出启动服务器的消息，显示当前加载的 mod
			WriteLineWithTimeStamp($"Starting dedicated server for mod: {modID}");
			
			// 无限循环，用于管理服务器的生命周期
			while (true)
			{
				// HACK: 确保引擎代码假设的 Game.ModData 被设置
				var modData = Game.ModData = new ModData(mods[modID], mods);
				modData.MapCache.LoadPreviewImages = false; // 优化性能：服务器不需要预览图，因此不加载它们以节省内存
				modData.MapCache.LoadMaps();

				// HACK: 初始化翻译提供者，以便加载带有翻译的地图和选项
				TranslationProvider.Initialize(modData, modData.DefaultFileSystem);

				// 配置服务器监听的 IP 端点（IPv4 和 IPv6）
				var endpoints = new List<IPEndPoint> { new(IPAddress.IPv6Any, settings.ListenPort), new(IPAddress.Any, settings.ListenPort) };
				var server = new Server(endpoints, settings, modData, ServerType.Dedicated);

				// 强制垃圾回收，以释放未使用的资源
				GC.Collect();

				// 内部循环，持续运行服务器实例
				while (true)
				{
					Thread.Sleep(1000); // 每秒检查一次服务器状态
					if (server.State == ServerState.GameStarted && server.Conns.Count < 1)
					{
						// 如果游戏已经开始但没有连接的客户端，关闭服务器
						WriteLineWithTimeStamp("No one is playing, shutting down...");
						server.Shutdown();
						break;
					}
				}

				// 释放 mod 数据的资源
				modData.Dispose();
				WriteLineWithTimeStamp("Starting a new server instance...");
			}
		}


		static void WriteLineWithTimeStamp(string line)
		{
			Console.WriteLine($"[{DateTime.Now.ToString(Game.Settings.Server.TimestampFormat, CultureInfo.CurrentCulture)}] {line}");
		}
	}
}
