using UnityEngine;
using UnityEditor;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public class AssetBundleManagerEditor
{
	private static string s_strExportPath = "Assets/StreamingAssets/";
	private static BuildAssetBundleOptions mOptions = BuildAssetBundleOptions.UncompressedAssetBundle|BuildAssetBundleOptions.CollectDependencies|BuildAssetBundleOptions.DeterministicAssetBundle;
	private static string strTargetExt = ".manifest";
	private static string strTargetZipExt = ".bytes";

	private static Dictionary<string, string> mAssetBundleDependDic = new Dictionary<string, string>(); /*文件名，文件MD5名*/

	[MenuItem("AssetBundle/Build/IOS")]
	public static void ExportIOSResource () 
	{
		RenameTagAll();

		BuildAssetResource(s_strExportPath + "iPhone/", BuildTarget.iOS);
	}

	[MenuItem("AssetBundle/Build/Android")]
	public static void ExportAndroidResource () 
	{
		RenameTagAll();

		BuildAssetResource(s_strExportPath + "Android/", BuildTarget.Android);
	}

	[MenuItem("AssetBundle/Build/StandaloneWindows")]
	public static void ExportWPResource () 
	{
		RenameTagAll();

		BuildAssetResource(s_strExportPath + "StandaloneWindows/", BuildTarget.StandaloneWindows);
	}

	public static void BuildAssetResource(string path, BuildTarget target)  
	{  
		if (Directory.Exists (path)) Directory.Delete (path, true);
		Directory.CreateDirectory (path);

		mAssetBundleDependDic.Clear(); /*首先清理*/

		// 平台字符串;
		string strTarget = "";
		if(target == BuildTarget.Android)
			strTarget = "Android";
		else if(target == BuildTarget.iOS)
			strTarget = "iPhone";
		else if(target == BuildTarget.StandaloneWindows)
			strTarget = "StandaloneWindows";

		// 生成assetbundle;
		AssetBundleManifest assetBundleManifest = BuildPipeline.BuildAssetBundles(path, mOptions, target); 

		// 压缩文件;
		string strdepAssetName = Path.GetFileNameWithoutExtension(AssetBundleManager.s_StrAssetBundleDescFile);
		Ionic.Zlib.ZlibStream.CompressFile(path + strTarget, path + "/" + strdepAssetName + strTargetZipExt);
		File.Delete(path + strTarget);
		Ionic.Zlib.ZlibStream.CompressFile(path + strTarget + strTargetExt, path + "/" + strdepAssetName + strTargetExt + strTargetZipExt);
		File.Delete(path + strTarget + strTargetExt);

		// 压缩文件;
		if(assetBundleManifest != null)
		{
			string[] allAssetBundles = assetBundleManifest.GetAllAssetBundles();
			for(int i = 0; i < allAssetBundles.Length; i++)
			{
				//Debug.Log("开始压缩文件: " + allAssetBundles[i]);
				ZipRes(path, allAssetBundles[i]);
			}
		}

		CreateXml(strTarget);

		AssetDatabase.Refresh(); 
	}

	private static void ZipRes(string strPath, string strResName)
	{
		string strMD5Text = CalcAssetMd5(strPath + strResName);
		mAssetBundleDependDic.Add(strResName, strMD5Text);
		//Debug.Log(strResName + "|" + strMD5Text);
		Ionic.Zlib.ZlibStream.CompressFile(strPath + strResName, strPath + "/" + strMD5Text + strTargetZipExt);
		File.Delete(strPath + strResName);

		string strMD5TextManifest = CalcAssetMd5(strPath + strResName + strTargetExt);
		mAssetBundleDependDic.Add(strResName + strTargetExt, strMD5TextManifest);
		//Debug.Log(strResName + strTargetExt + "|" + strMD5TextManifest);
		Ionic.Zlib.ZlibStream.CompressFile(strPath + strResName + strTargetExt, strPath + "/" + strMD5TextManifest + strTargetZipExt);
		File.Delete(strPath + strResName + strTargetExt);
	}

	[MenuItem("AssetBundle/RenameTag/Bytes")]
	public static void RenameTag_Bytes () 
	{
		SetAssetNameInChildDir("Assets/Artwork/Download/Bytes/", "txt", "asset_bytes");
		SetAssetNameInChildDir("Assets/Artwork/Download/Bytes/", "bytes", "asset_bytes");
	}

	[MenuItem("AssetBundle/RenameTag/Texture")]
	public static void RenameTag_Texture () 
	{
		SetAssetNameInChildDir("Assets/Artwork/Download/Texture/", "png", "asset_texture");
		SetAssetNameInChildDir("Assets/Artwork/Download/Texture/", "jpg", "asset_texture");
	}

	[MenuItem("AssetBundle/RenameTag/Prefab")]
	public static void RenameTag_Prefab () 
	{
		SetAssetNameInChildDir("Assets/Artwork/Download/Prefab/", "prefab", "asset_prefab");
	}

	[MenuItem("AssetBundle/RenameTag/Sound")]
	public static void RenameTag_Sound () 
	{
		SetAssetNameInChildDir("Assets/Artwork/Download/Sound/", "ogg", "asset_sound");
		SetAssetNameInChildDir("Assets/Artwork/Download/Sound/", "mp3", "asset_sound");
	}

	[MenuItem("AssetBundle/RenameTag/Scene")]
	public static void RenameTag_Scene () 
	{
		SetAssetNameInChildDir("Assets/Artwork/Download/Scene/", "unity", "asset_scene");
	}

	[MenuItem("AssetBundle/RenameTagAll")]
	public static void RenameTagAll()
	{
		RenameTag_Bytes ();
		RenameTag_Texture ();
		RenameTag_Prefab ();
		RenameTag_Sound ();
		RenameTag_Scene ();
	}

	public static void SetAssetNameInChildDir(string strDir, string strExt, string assetBundleName)
	{
		string strApplicationDir = Application.dataPath;
		DirectoryInfo dirInfo = new DirectoryInfo(strDir);

		foreach (var fileInfo in dirInfo.GetFiles("*." + strExt, System.IO.SearchOption.AllDirectories))
		{
			string strUsePath = fileInfo.FullName;
			string strFileName = fileInfo.Name;

			strUsePath = strUsePath.Replace("\\", "/");
			strUsePath = strUsePath.Replace(strApplicationDir + "/", "");
			strUsePath = strUsePath.Replace("/" + strFileName, "");

			SetVersionDirAssetName(strUsePath, assetBundleName);
		}
	}

	public static void SetVersionDirAssetName(string versionDir, string assetBundleName)
	{
		var fullPath = Application.dataPath + "/" + versionDir + "/";
		var relativeLen = versionDir.Length + 8; // Assets 长度
		if (Directory.Exists(fullPath))
		{
			EditorUtility.DisplayProgressBar("设置AssetName名称", "正在设置AssetName名称中...", 0f);
			var dir = new DirectoryInfo(fullPath);
			var files = dir.GetFiles("*", SearchOption.AllDirectories);
			for (var i = 0; i < files.Length; ++i)
			{
				var fileInfo = files[i];
				EditorUtility.DisplayProgressBar("设置AssetName名称", "正在设置AssetName名称中...", 1f * i / files.Length);
			
				if (!fileInfo.Name.EndsWith(".meta"))
				{
					var basePath = fileInfo.FullName.Substring(fullPath.Length - relativeLen).Replace('\\', '/');
					var importer = AssetImporter.GetAtPath(basePath);
					if (importer /*&& importer.assetBundleName != versionDir*/)
					{
						if(fileInfo.Name.Contains(".unity"))
						{
							importer.assetBundleName = assetBundleName + "_" + fileInfo.Name.Replace(".unity", "");
						}
						else
						{
							importer.assetBundleName = assetBundleName; // + "_" + fileInfo.Name.Replace(".unity", "");	
						}
					}
				}
			}
			EditorUtility.ClearProgressBar();
		}
	}


	public static string CalcAssetMd5(string strAssetPath)
	{
		byte[] szAssetBytes = File.ReadAllBytes(strAssetPath);
		MD5 md5Hash = MD5.Create();
		byte[] szMd5Value = md5Hash.ComputeHash(szAssetBytes);

		StringBuilder sBuilder = new StringBuilder();
		for (int i = 0; i < szMd5Value.Length; i++)
		{
			sBuilder.Append(szMd5Value[i].ToString("x2"));
		}

		return sBuilder.ToString();
	}

	public static long FileSize(string filePath)
	{
		long temp = 0;

		//判断当前路径所指向的是否为文件
		if (File.Exists(filePath) == false)
		{
			string[] str1 = Directory.GetFileSystemEntries(filePath);
			foreach (string s1 in str1)
			{
				temp += FileSize(s1);
			}
		}
		else
		{

			//定义一个FileInfo对象,使之与filePath所指向的文件向关联,

			//以获取其大小
			FileInfo fileInfo = new FileInfo(filePath);
			return fileInfo.Length;
		}
		return temp;
	}

	public static void CreateXml(string strPath)
	{
		string filepath = Application.dataPath + "/StreamingAssets/" + strPath + @"/assetBundleDepend.xml";

		if(File.Exists (filepath))
			File.Delete(filepath);

		XmlDocument xmlDoc = new XmlDocument();
		XmlElement root = xmlDoc.CreateElement("AssetBundleDepend");

		foreach(var temp in mAssetBundleDependDic)
		{
			XmlElement elm = xmlDoc.CreateElement("FILE");
			elm.SetAttribute("NAME", temp.Key);
			XmlElement value = xmlDoc.CreateElement("VALUE");
			value.InnerText = temp.Value;
			XmlElement value2 = xmlDoc.CreateElement("SIZE");
			string strFileSize = FileSize(Application.dataPath + "/StreamingAssets/" + strPath + @"/" + temp.Value + strTargetZipExt).ToString();
			value2.InnerText = strFileSize;
			elm.AppendChild(value);
			elm.AppendChild(value2);
			root.AppendChild(elm);
		}

		xmlDoc.AppendChild(root);
		xmlDoc.Save(filepath);

		// 压缩文件;
		Ionic.Zlib.ZlibStream.CompressFile(filepath, Application.dataPath + "/StreamingAssets/" + strPath + @"/assetBundleDepend" + strTargetZipExt);
		File.Delete(filepath);
	}

	public static void ShowXml(string strPath)
	{
		string filepath = Application.dataPath + "/StreamingAssets/" + strPath + @"/assetBundleDepend.xml";

		if(File.Exists (filepath))
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.Load(filepath);
			XmlNodeList nodeList=xmlDoc.SelectSingleNode("AssetBundleDepend").ChildNodes;

			// 遍历每一个节点，拿节点的属性以及节点的内容;
			foreach(XmlElement xe in nodeList)
			{
				//Debug.Log("Attribute :" + xe.GetAttribute("NAME"));
				foreach(XmlElement x1 in xe.ChildNodes)
				{
					if(x1.Name == "VALUE")
					{
						Debug.Log("VALUE :" + x1.InnerText);
					}
				}
			}
		}
	}
}
