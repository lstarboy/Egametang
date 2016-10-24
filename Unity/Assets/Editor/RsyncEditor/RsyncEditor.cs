﻿using System.Diagnostics;
using System.IO;
using Base;
using UnityEditor;
using UnityEngine;

namespace MyEditor
{
	public class RsyncEditor : EditorWindow
	{
		private const string ConfigFile = @"..\Config\Rsync\rsyncConfig.txt";
		private RsyncConfig rsyncConfig;
		private bool isFold = true;

		[MenuItem("Tools/Rsync同步")]
		private static void ShowWindow()
		{
			GetWindow(typeof(RsyncEditor));
		}

		[MenuItem("Tools/Rsync同步")]
		private static void ShowTool()
		{
			GetWindow(typeof(RsyncEditor));
		}

		private void OnEnable()
		{
			if (!File.Exists(ConfigFile))
			{
				this.rsyncConfig = new RsyncConfig();
				return;
			}
			string s = File.ReadAllText(ConfigFile);
			this.rsyncConfig = MongoHelper.FromJson<RsyncConfig>(s);
		}

		private void OnGUI()
		{
			rsyncConfig.Host = EditorGUILayout.TextField("服务器地址", rsyncConfig.Host);
			rsyncConfig.Account = EditorGUILayout.TextField("账号（必须是Linux已有的账号）", rsyncConfig.Account);
			rsyncConfig.Password = EditorGUILayout.TextField("密码", rsyncConfig.Password);
			rsyncConfig.RelativePath = EditorGUILayout.TextField("相对路径", rsyncConfig.RelativePath);

			this.isFold = EditorGUILayout.Foldout(isFold, $"排除列表:");

			if (!this.isFold)
			{
				for (int i = 0; i < this.rsyncConfig.Exclude.Count; ++i)
				{
					GUILayout.BeginHorizontal();
					this.rsyncConfig.Exclude[i] = EditorGUILayout.TextField(this.rsyncConfig.Exclude[i]);
					if (GUILayout.Button("删除"))
					{
						this.rsyncConfig.Exclude.RemoveAt(i);
						break;
					}
					GUILayout.EndHorizontal();
				}
			}

			if (GUILayout.Button("添加排除项目"))
			{
				this.rsyncConfig.Exclude.Add("");
			}

			if (GUILayout.Button("保存"))
			{
				File.WriteAllText(ConfigFile, MongoHelper.ToJson(this.rsyncConfig));
				using (StreamWriter sw = new StreamWriter(new FileStream(@"..\Config\Rsync\rsync.secrets", FileMode.Create)))
				{
					foreach (string s in this.rsyncConfig.Exclude)
					{
						sw.Write(s + "\n");
					}
				}

				File.WriteAllText($@"..\Config\Rsync\rsync.secrets", this.rsyncConfig.Password);
				File.WriteAllText($@"..\Config\Rsync\rsyncd.secrets", $"{this.rsyncConfig.Account}:{this.rsyncConfig.Password}");

				string rsyncdConf =
					"uid = root\n" +
					"gid = root\n" +
					"use chroot = no\n" +
					"max connections = 100\n" +
					"read only = no\n" +
					"write only = no\n" +
					"log file =/var/log/rsyncd.log\n" +
					"[Upload]\n" +
					$"path = /home/{this.rsyncConfig.Account}/\n" +
					$"auth users = {this.rsyncConfig.Account}\n" +
					"secrets file = /etc/rsyncd.secrets\n" +
					"list = yes";
				File.WriteAllText($@"..\Config\Rsync\rsyncd.conf", rsyncdConf);
			}

			if (GUILayout.Button("同步"))
			{
				string arguments = $"-vzrtopg --password-file=./Config/Rsync/rsync.secrets --exclude-from=./Config/Rsync/exclude.txt --delete ./ {this.rsyncConfig.Account}@{this.rsyncConfig.Host}::Upload/{this.rsyncConfig.RelativePath} --chmod=ugo=rwX";
				ProcessStartInfo startInfo = new ProcessStartInfo();
				startInfo.FileName = @".\Tools\cwRsync\rsync.exe";
				startInfo.Arguments = arguments;
				startInfo.UseShellExecute = true;
				startInfo.WorkingDirectory = @"..\";
				Process p = Process.Start(startInfo);
				p.WaitForExit();
				Log.Info("同步完成!");
			}
		}
	}
}
