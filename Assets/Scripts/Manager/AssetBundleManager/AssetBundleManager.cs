//#undef UNITY_EDITOR
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mono.Xml;
using System.Xml;
using System.Xml.Serialization;
using System.Net;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum enResType
{
	enResType_Default,
	enResType_Texture,
	enResType_Bytes,
	enResType_Prefab,
	enResType_Sound,
	enResType_Scene,
};

public class DownLoadProgress
{
	public int iFileIndex = 0;  /*文件索引*/
	public int iTotalFileCount = 0;  /*全部文件总数*/
	public long fDownLoadBytes = 0; /*下载的字节数*/
	public long fTotalBytes = 0; /*总字节数*/
	public decimal fProgress = 0; /*下载进度*/
	public bool bDownloadFinsih = false; /*下载是否结束*/
	public bool bDownload = false;
	public bool bCompressing = false; /*资源解压中*/
	public bool bCompressFinish = false; /*解压完毕*/
};

public static class AssetBundleManager
{
	public delegate void AssetDelegate(Object resultObject, string strErrMsg);
	public delegate void AssetBundleInitFinish(bool bOK, string strErrMsg);
	public delegate void AssetBundleProgress(DownLoadProgress downLoadProgress);

	private static string s_strAssetBundleExt = ".bytes";
	private static string s_strPreHttpDirectory = "";
	private static string s_strRealHttpDirectory = "";
	private static string s_strStreamAssetDir = Application.streamingAssetsPath;
	private static string s_strPersistUrlPath = "file:///" + Application.persistentDataPath;
	private static string s_strPersistPath = Application.persistentDataPath;
	private static bool s_bInitlized = false;
	private static HashSet<string> s_assetBundleList = new HashSet<string> ();
	private static string[] s_allAssetBundlesArray = null;
	private static Dictionary<string, string> s_asset2BundleMap = new Dictionary<string, string>();
	private static AssetBundle s_assetBundle = null;
	private static AssetBundle s_assetBundleScene = null;

	static Dictionary<string, List<string> > s_assetBundleAsstList;
	static Dictionary<string, string> s_assetBundleDependency;

	static Dictionary<string, string> s_assetBundleListNew;
	static Dictionary<string, string> s_assetBundleListOld;

	static Dictionary<string, string> s_assetBundleSizeListNew;
	static Dictionary<string, string> s_assetBundleSizeListOld;

	static HashSet<string> s_levelList;
	static HashSet<string> s_preloadBundleList;
	static Dictionary<string, GameObject> s_directGameObject = new Dictionary<string, GameObject>();
	static Dictionary<string, Object> s_cachedObject = new Dictionary<string,Object>(); // Cache过的Obj;
	static Dictionary<string, AssetBundle> _cachedAssetBundleMap = new Dictionary<string, AssetBundle>();
	static Dictionary<string, AssetBundle> s_preloadBundleMap = new Dictionary<string, AssetBundle>();

	public static string s_StrAssetBundleDescFile = "assetBundleManifest.bytes";
	public static string s_StrAssetBundleDependFile = "assetBundleDepend.bytes";
	private static int s_DownloadSize = 1024 * 1;

	#if UNITY_EDITOR
	private static Dictionary<string, string> s_assetPathDic = new Dictionary<string, string>();
	#endif

	public static GameObject getDirectObject(string strAssetName)
	{
		GameObject outValue = null;
		if (s_directGameObject.TryGetValue(strAssetName, out outValue))
		{
			return outValue;
		}

		return null;
	}

	/// <summary>
	/// 根据名字获取获取实例对象.
	/// </summary>
	/// <returns>Object.</returns>
	/// <param name="strAsstName">名字.</param>
	public static Object GetAssetObject(enResType type, string strAsstName = "")
	{
		#if UNITY_EDITOR
		if(type != enResType.enResType_Scene)
		{
			// 优先DirectObject
			var directObj = getDirectObject(strAsstName);
			if (directObj != null) return directObj;

			string strPath;
			if(s_assetPathDic != null && s_assetPathDic.ContainsKey(strAsstName))
			{
				if(s_assetPathDic.TryGetValue(strAsstName, out strPath) )
				{
					var retObj = AssetDatabase.LoadAssetAtPath(strPath, typeof(UnityEngine.Object));
					return retObj;
				}
				else
				{
					Debug.LogWarning(string.Format("没有配置资源 : {0}", strAsstName));
					return null;
				}
			}
			else
			{
				Debug.LogWarning(string.Format("没有配置资源 : {0}", strAsstName));
				return null;
			}	
		}
		else
		{
			return null;
		}
		#else
		if(type != enResType.enResType_Scene)
		{

			if(strAsstName != null && strAsstName.Length == 0)
			{
				Debug.LogWarning("strAsstName is null");
				return null;	
			}

			// 优先DirectObject;
			var directObj = getDirectObject(strAsstName.ToLower());
			if (directObj != null) return directObj;

			// first find in pool;
			Object proto = null;
			if (s_cachedObject.TryGetValue(strAsstName.ToLower(), out proto))
			{
				//Debug.Log("Find Cache AssetObj:" + strAsstName);
				return proto;
			}

			// 先找到它在哪个AssetBundle里;
			string strAssetBundleName;
			if (!s_asset2BundleMap.TryGetValue(strAsstName.ToLower(), out strAssetBundleName))
			{
				Debug.LogError("未找到["+ strAsstName.ToLower() +"]依赖的strAsstName");
				return null;
			}

			Object ret = createAssetFromBundle (strAssetBundleName, strAsstName.ToLower());

			return ret;
		}
		else
		{
			string levelKey = "asset_scene_" + strAsstName.ToLower();
			string realFileName = null;
			if(s_assetBundleListNew.ContainsKey(levelKey))
				s_assetBundleListNew.TryGetValue(levelKey, out realFileName);
			if(realFileName != null && realFileName.Length > 0)
			{
				string filePath = System.IO.Path.Combine(s_strPersistPath, realFileName + s_strAssetBundleExt);
				if(s_assetBundleScene != null)
					s_assetBundleScene.Unload(true);
				s_assetBundleScene = AssetBundle.LoadFromFile(filePath);
				return (Object)s_assetBundleScene;
			}
			return null;
		}
		#endif	
	}

	static Object createAssetFromBundle(string strAssetBundle, string strAssetName)
	{
		// 准备创建了; 
		float begin = Time.realtimeSinceStartup;
		AssetBundle target = createAssetBundle(strAssetBundle);
		Object retObject = target.LoadAsset(strAssetName);

		//Debug.Log("----CreateBundle|" + strAssetName + "|" + (Time.realtimeSinceStartup - begin));
		//ClearBundle();

		s_cachedObject[strAssetName] = retObject;

		return retObject;
	}

	static AssetBundle createAssetBundle(string strAssetBundle)
	{
		AssetBundle target = null;
		if (_cachedAssetBundleMap.TryGetValue(strAssetBundle, out target))
		{
			return target;
		}

		if(s_preloadBundleMap.TryGetValue(strAssetBundle, out target) )
		{
			return target;
		}

		List<string> assetBundleList = new List<string>();
		assetBundleList.Add(strAssetBundle);

		string strTmpBkAsstBandle = strAssetBundle;
		string strDependenctyName;
		while (s_assetBundleDependency.TryGetValue(strTmpBkAsstBandle, out strDependenctyName))
		{
			assetBundleList.Add(strDependenctyName);
			strTmpBkAsstBandle = strDependenctyName;
		}

		assetBundleList.Reverse();

		foreach (string strLoadAssetBundle in assetBundleList)
		{
			if (_cachedAssetBundleMap.ContainsKey(strLoadAssetBundle))
			{
				continue;
			}

			if (s_preloadBundleMap.ContainsKey(strLoadAssetBundle))
			{
				continue;
			}

			if (!s_assetBundleList.Contains(strLoadAssetBundle))
			{
				Debug.LogError("AssetBundle:" + strLoadAssetBundle + "不存在!!");
				return null;
			}

			//  直接从PersistContentPath 读取;
			//float begin = Time.realtimeSinceStartup;

			string filePath = System.IO.Path.Combine(s_strPersistPath, strLoadAssetBundle + s_strAssetBundleExt);
			AssetBundle depBundle = AssetBundle.LoadFromFile(filePath);

			_cachedAssetBundleMap[strLoadAssetBundle] = depBundle;
			target = depBundle;
			//Debug.Log("LoadAssetBundleInternal|" + strLoadAssetBundle + "|" + (Time.realtimeSinceStartup - begin));
		}

		return target;
	}

	/// <summary>
	/// 主要PC应用的接口，用于PC上资源获取.
	/// </summary>
	/// <param name="strDir">目录.</param>
	/// <param name="strExt">后缀.</param>
	/// <param name="hasChildDir">If set to <c>true</c> has child dir.</param>
	private static void AddDirBinaryFilesToAsset(string strDir, string strExt, bool hasChildDir = false)
	{
		#if UNITY_EDITOR
		string strProjectPath = Directory.GetParent(Application.dataPath).FullName;
		strProjectPath = strProjectPath.Replace("\\", "/");
		if(strProjectPath[strProjectPath.Length - 1] != '/')
		{
			strProjectPath = strProjectPath + "/";
		}

		DirectoryInfo dirInfo = new DirectoryInfo (strDir);
		if(hasChildDir)
		{
			foreach (var dirInfoItem in dirInfo.GetDirectories()) 
			{
				string dirName = dirInfoItem.Name;
				if(dirName != null && dirName.Length > 0)
				{
					AddDirBinaryFilesToAsset(strDir + dirName + "/" , strExt, false);
				}
			}
		}
		else
		{
			foreach (var fileInfo in dirInfo.GetFiles("*." + strExt, System.IO.SearchOption.AllDirectories)) 
			{
				string strName = Path.GetFileNameWithoutExtension(fileInfo.Name);
				string strUsePath = fileInfo.FullName;
				strUsePath = strUsePath.Replace("\\", "/");
				strUsePath = strUsePath.Replace(strProjectPath, "");

				if (s_assetPathDic.ContainsKey(strName) )
				{
					Debug.LogError("存在多个相同名字的资源:" + strName);
				}

				s_assetPathDic[strName] = strUsePath;
			}
		}
		#endif
	}

	private static void AddAsset2BundleMap(string strpath, string bundleName)
	{
		//Debug.Log("strpath : " + strpath + " bundleName ：" + bundleName);
		AssetBundle depBundleMap = AssetBundle.LoadFromFile(strpath);
		if(depBundleMap != null)
		{
			string[] allAssetNames = depBundleMap.GetAllAssetNames();
			for(int j = 0; j < allAssetNames.Length; j++)
			{
				string assetName = Path.GetFileNameWithoutExtension(allAssetNames[j]);
				if(assetName != null && assetName.Length > 0)
				{
					if(!s_asset2BundleMap.ContainsKey(assetName))
					{
						s_asset2BundleMap.Add(assetName, bundleName);	
					}
					else 
					{
						//						string strOldBund = null;
						//						s_asset2BundleMap.TryGetValue(assetName, out strOldBund);
						//						Debug.Log(assetName + "|" + bundleName + "|" + strOldBund);
					}
				}
			}

			depBundleMap.Unload(true);
		}
	}

	private static void CacheXml(string filepath, bool isOld)
	{
		if(File.Exists (filepath))
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.Load(filepath);
			XmlNodeList nodeList=xmlDoc.SelectSingleNode("AssetBundleDepend").ChildNodes;

			// 遍历每一个节点，拿节点的属性以及节点的内容;
			foreach(XmlElement xe in nodeList)
			{
				foreach(XmlElement x1 in xe.ChildNodes)
				{
					string strName = xe.GetAttribute("NAME");
					if(x1.Name == "VALUE")
					{
						string strValue = x1.InnerText;

						if(isOld)
							s_assetBundleListOld.Add(strName, strValue);
						else
							s_assetBundleListNew.Add(strName, strValue);
					}
					else if(x1.Name == "SIZE")
					{
						string strValue = x1.InnerText;

						if(isOld)
							s_assetBundleSizeListOld.Add(strName, strValue);
						else
							s_assetBundleSizeListNew.Add(strName, strValue);
					}
				}
			}

			if(isOld)
				System.IO.File.Delete(filepath);
		}
	}

	/// <summary>
	/// Real 下载.
	/// </summary>
	/// <returns>下载协程.</returns>
	/// <param name="initCb">下载结束回调函数.</param>
	/// <param name="progressCb">下载进度回调函数.</param>
	private static IEnumerator _initlizeDependData(AssetBundleInitFinish initCb, AssetBundleProgress progressCb)
	{
		s_asset2BundleMap = new Dictionary<string,string>();
		s_assetBundleAsstList = new Dictionary<string, List<string>>();
		s_assetBundleDependency = new Dictionary<string,string>();
		s_assetBundleList = new HashSet<string> ();
		s_assetBundleListNew = new Dictionary<string, string>();
		s_assetBundleListOld = new Dictionary<string, string>();
		s_assetBundleSizeListNew = new Dictionary<string, string>();
		s_assetBundleSizeListOld = new Dictionary<string, string>();
		s_levelList = new HashSet<string> ();
		s_preloadBundleList = new HashSet<string>();

		byte[] szResultObjBytes = null;

		double dSecond = (System.DateTime.Now - new System.DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds;
		string strRemoteXmlFile = s_strPreHttpDirectory + "/" + s_StrAssetBundleDescFile + "?time=" + dSecond;
		string strDependXmlName = Path.GetFileNameWithoutExtension(s_StrAssetBundleDependFile);
		string strDependXmlFile = s_strPreHttpDirectory + "/" + s_StrAssetBundleDependFile + "?time=" + dSecond;

		//Debug.Log("strRemoteXmlFile : " + strRemoteXmlFile);
		//Debug.Log("strDependXmlFile : " + strDependXmlFile);

		// 缓存映射文件;
		CacheXml(s_strPersistPath + "/" + strDependXmlName + ".xml", true);

		//		foreach(var temp in s_assetBundleListOld)
		//		{
		//			Debug.Log(temp.Key + "|" + temp.Value);
		//		}

		using (WWW dependXmlDownload = new WWW(strDependXmlFile))
		{
			yield return dependXmlDownload;

			if (dependXmlDownload.error != null && dependXmlDownload.error.Length > 0)
			{
				Debug.LogError("AssetBundleDepend Init Fail:" + dependXmlDownload.error);

				DownLoadProgress downLoadProgressError = new DownLoadProgress();
				downLoadProgressError.bDownload = false;
				downLoadProgressError.bDownloadFinsih = true;

				initCb(false, "Get Resource Depend Object Fail:" + dependXmlDownload.error);
				yield break;
			}
			else
			{
				//Debug.Log("Load Xml Success|" + dependXmlDownload.bytes.Length);
				Ionic.Zlib.ZlibStream.DeCompressBuffToFile(dependXmlDownload.bytes, s_strPersistPath + "/" + strDependXmlName + ".xml");

				CacheXml(s_strPersistPath + "/" + strDependXmlName + ".xml", false);

				//				foreach(var temp in s_assetBundleListNew)
				//				{
				//					Debug.Log(temp.Key + "|" + temp.Value);
				//				}
			}
		}

		using (WWW xmlDownload = new WWW(strRemoteXmlFile))
		{
			yield return xmlDownload;

			if (xmlDownload.error != null && xmlDownload.error.Length > 0)
			{
				Debug.LogError("AssetBundle Init Fail:" + xmlDownload.error);

				DownLoadProgress downLoadProgressError = new DownLoadProgress();
				downLoadProgressError.bDownload = false;
				downLoadProgressError.bDownloadFinsih = true;

				progressCb(downLoadProgressError); /*出错了 这里显示进度为解压中*/
				initCb(false, "Get Resource Depend Object Fail:" + xmlDownload.error);
				yield break;
			}
			else
			{
				//Debug.Log("Load Xml Success|" + xmlDownload.bytes.Length);
				szResultObjBytes = xmlDownload.bytes;
			}

			byte[] decompressResult = Ionic.Zlib.ZlibStream.UncompressBuffer(szResultObjBytes);
			AssetBundle depBundle = AssetBundle.LoadFromMemory(decompressResult);
			string strDepName = Path.GetFileNameWithoutExtension(s_StrAssetBundleDescFile);
			AssetBundleManifest assetBundleManifest = depBundle.LoadAsset<AssetBundleManifest>(strDepName);
			string[] allAssetBundles = assetBundleManifest.GetAllAssetBundles();

			for(int i = 0; i< allAssetBundles.Length; i++)
			{
				if(s_assetBundleListNew.ContainsKey(allAssetBundles[i]))
				{
					string realFileName = null;
					s_assetBundleListNew.TryGetValue(allAssetBundles[i], out realFileName);
					s_assetBundleList.Add(realFileName);	

					//Debug.Log("allAssetBundles[i] : " + allAssetBundles[i] + " realFileName : " + realFileName);
				}
			}

			depBundle.Unload(true);

			// 清理目录下不需要的文件(变动的文件需要清理掉重新下载);
			HashSet<string> delFileList = new HashSet<string>();
			DirectoryInfo dirInfo = new DirectoryInfo(s_strPersistPath);
			foreach (var fileInfo in dirInfo.GetFiles("*.bytes", System.IO.SearchOption.AllDirectories))
			{
				string strFileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(fileInfo.FullName);
				if( (fileInfo.Length == 0) || !s_assetBundleList.Contains(strFileNameNoExt) )
				{
					delFileList.Add(fileInfo.FullName);
				}
			}

			foreach (var item in delFileList)
			{
				System.IO.File.Delete(item);
				//Debug.Log("Delete File|" + item);
			}

			int iDownloadCount = allAssetBundles.Length;
			int iProgress = 0;
			int iLocalCount = 0;
			for(int i = 0; i < iDownloadCount; i++)
			{
				// 获取文件真正的名字;
				string realName = null;
				if(s_assetBundleListNew.ContainsKey(allAssetBundles[i]))
					s_assetBundleListNew.TryGetValue(allAssetBundles[i], out realName);

				string realSize = null;
				if(s_assetBundleSizeListNew.ContainsKey(allAssetBundles[i]))
					s_assetBundleSizeListNew.TryGetValue(allAssetBundles[i], out realSize);

				if(realName == null || (realName != null && realName.Length <= 0)) Debug.LogError("ERROR !!!!");

				// 检查本地是否存在以下文件;
				string strPersistPath = s_strPersistPath + "/" + realName + s_strAssetBundleExt;
				if (System.IO.File.Exists(strPersistPath))
				{
					iLocalCount++;

					// 添加映射文件;
					AddAsset2BundleMap(strPersistPath, realName);

					continue;
				}

				// 检查程序包中是否存在文件;
				string strLocalPath = System.IO.Path.Combine(s_strStreamAssetDir, realName + s_strAssetBundleExt);
				using (WWW www = new WWW(strLocalPath))
				{
					yield return www;

					if(www.error != null && www.error.Length > 0)
					{
						//iLocalCount = 0;		
						string strAssetBundleFile = s_strRealHttpDirectory + "/" + realName + s_strAssetBundleExt;

						// 按照策划要求，先暴力处理,不停的尝试;                     
						while (true)
						{
							using (WWW download = new WWW(strAssetBundleFile))
							{
								DownLoadProgress downLoadProgress = new DownLoadProgress();
								downLoadProgress.bDownload = true;
								downLoadProgress.iFileIndex = iLocalCount;
								downLoadProgress.iTotalFileCount = iDownloadCount;

								while(!download.isDone)
								{
									downLoadProgress.fDownLoadBytes = (long)(long.Parse(realSize) * download.progress);
									downLoadProgress.fTotalBytes = long.Parse(realSize);
									downLoadProgress.fProgress = (decimal)download.progress * 100;
									downLoadProgress.bDownloadFinsih = false;
									downLoadProgress.bCompressing = false;
									downLoadProgress.bCompressFinish = false;

									progressCb(downLoadProgress);

									yield return 1;
								}

								downLoadProgress.fDownLoadBytes = long.Parse(realSize);
								downLoadProgress.fTotalBytes = long.Parse(realSize);
								downLoadProgress.fProgress = 100;
								downLoadProgress.bDownloadFinsih = true;
								downLoadProgress.bCompressing = true;
								downLoadProgress.bCompressFinish = false;

								progressCb(downLoadProgress);
									
								if (download.error != null && download.error.Length > 0)
								{
									yield return new WaitForSeconds(0.1f);
									continue;
								}

								Debug.Log("Download: "+ s_strPersistPath + "/" + realName + s_strAssetBundleExt);
								Debug.Log("Size: " + download.bytes.Length);
                                Ionic.Zlib.ZlibStream.DeCompressBuffToFile(download.bytes, s_strPersistPath + "/" + realName + s_strAssetBundleExt);

								// 添加映射文件;
								AddAsset2BundleMap(strPersistPath, realName);

								// 解压完;
								DownLoadProgress downLoadCompress = new DownLoadProgress();
								downLoadCompress.bDownload = true;
								downLoadCompress.iFileIndex = iLocalCount;
								downLoadCompress.iTotalFileCount = iDownloadCount;
								downLoadCompress.fDownLoadBytes = downLoadProgress.fDownLoadBytes;
								downLoadCompress.fTotalBytes = downLoadProgress.fTotalBytes;
								downLoadCompress.fProgress = 100;
								downLoadCompress.bDownloadFinsih = true;
								downLoadCompress.bCompressing = true;
								downLoadCompress.bCompressFinish = true;
								progressCb(downLoadCompress);
							}
							break;
						}
					}
					else
					{
						Debug.Log("DeCompressBuffToFile !!!!");

						// 解压完;
						DownLoadProgress downLoadCompress = new DownLoadProgress();
						downLoadCompress.bDownload = true;
						downLoadCompress.bDownloadFinsih = true;
						downLoadCompress.fProgress = (decimal)(iLocalCount / (float)iDownloadCount) * 100;
						downLoadCompress.bCompressing = true;
						downLoadCompress.bCompressFinish = false;
						progressCb(downLoadCompress);

                        Debug.Log("iLocalCount:" + iLocalCount + "/" + iDownloadCount + " Export:" + s_strPersistPath + "/" + realName + s_strAssetBundleExt);
                        Debug.Log("size:" + www.bytes.Length);
                        Ionic.Zlib.ZlibStream.DeCompressBuffToFile(www.bytes, s_strPersistPath + "/" + realName + s_strAssetBundleExt);

                        Debug.Log("解压成功");

                        iLocalCount++;

						// 添加映射文件;
						AddAsset2BundleMap(strPersistPath, realName);

						if(iLocalCount == iDownloadCount)
						{
							downLoadCompress.fProgress = 100;
							downLoadCompress.bCompressing = true;
							downLoadCompress.bCompressFinish = true;
							progressCb(downLoadCompress);
						}

						continue;
					}
				}

				iLocalCount++;
			}
		}

		DownLoadProgress downLoadProgressLocal = new DownLoadProgress();
		downLoadProgressLocal.bDownload = false;
		downLoadProgressLocal.bDownloadFinsih = true;
		progressCb(downLoadProgressLocal);

		initCb(true, "");
	}

	/// <summary>
	/// 初始化资源管理器.
	/// </summary>
	/// <param name="strUrlDirectory">下载地址.</param>
	/// <param name="initCb">结束下载.</param>
	/// <param name="progressCb">进度.</param>
	public static void InitlizeAssetBundle(string strPreUrlDirectory, string strRealUrlDirectory, AssetBundleInitFinish initCb, AssetBundleProgress progressCb)
	{
		if (s_bInitlized)
		{
			DownLoadProgress downLoadProgress = new DownLoadProgress();
			downLoadProgress.bDownload = false;
			downLoadProgress.bDownloadFinsih = true;
			progressCb(downLoadProgress);
			//progressCb(100, false);
			initCb(true, "");
			return;
		}

		s_strPreHttpDirectory = strPreUrlDirectory;
		s_strRealHttpDirectory = strRealUrlDirectory;

		#if UNITY_STANDALONE_WIN
		s_strPreHttpDirectory += "/StandaloneWindows/";
		s_strRealHttpDirectory += "/StandaloneWindows/";
		s_strStreamAssetDir += "/StandaloneWindows/";

		#elif UNITY_ANDROID
		s_strPreHttpDirectory += "/Android/";
		s_strRealHttpDirectory += "/Android/";
		s_strStreamAssetDir += "/Android/";

		#elif UNITY_IPHONE
		s_strPreHttpDirectory += "/iPhone/";
		s_strRealHttpDirectory += "/iPhone/";
		s_strStreamAssetDir = "file://" + Application.dataPath + "/Raw/iPhone/";

		#elif UNITY_WP8
		s_strPreHttpDirectory += "/WP8Player/";
		s_strRealHttpDirectory += "/WP8Player/";
		s_strStreamAssetDir += "/WP8Player/";
		#elif UNITY_METRO
		s_strPreHttpDirectory += "/MetroPlayer";
		s_strRealHttpDirectory += "/MetroPlayer";
		s_strStreamAssetDir += "/MetroPlayer";
		#endif

		#if UNITY_EDITOR
		s_strStreamAssetDir = "file:///" + s_strStreamAssetDir;
		s_strPersistUrlPath = "file:///" + Application.dataPath;	
		s_strPersistPath = Application.dataPath;
		#endif

		#if UNITY_EDITOR

		// 标记已初始化;
		s_bInitlized = true;
	
		// bin文件加载;
		AddDirBinaryFilesToAsset("Assets/Artwork/Download/Bytes/", "bytes", false);
		AddDirBinaryFilesToAsset("Assets/Artwork/Download/Bytes/", "txt", false);

		// Texture文件加载;
		AddDirBinaryFilesToAsset("Assets/Artwork/Download/Texture/", "png", false);
		AddDirBinaryFilesToAsset("Assets/Artwork/Download/Texture/", "jpg", false);

		// Prefab文件加载;
		AddDirBinaryFilesToAsset("Assets/Artwork/Download/Prefab/", "prefab", false);

		// 场景文件加载;
		AddDirBinaryFilesToAsset("Assets/Artwork/Download/Scene/", "unity", false);

		// 声音文件加载;
		AddDirBinaryFilesToAsset("Assets/Artwork/Download/Sound/", "mp3", false);
		AddDirBinaryFilesToAsset("Assets/Artwork/Download/Sound/", "ogg", false);

		DownLoadProgress downLoadProgresst = new DownLoadProgress();
		downLoadProgresst.bDownload = false;
		downLoadProgresst.bDownloadFinsih = true;
		progressCb(downLoadProgresst);
       // progressCb(100, false);
		initCb(true, "");
		#else
		//Debug.Log ("StreamAsset Path:" + s_strStreamAssetDir);
		//Debug.Log ("PersistPath:" + Application.persistentDataPath);

		Coroutiner.StartCoroutine(_initlizeBaseContext(initCb, progressCb));
		#endif
	}
	
	/// <summary>
	/// 下载协程;
	/// </summary>
	/// <returns>The base context.</returns>
	/// <param name="initCb">下载结束进度.</param>
	/// <param name="progressCb">下载进度.</param>
	private static IEnumerator _initlizeBaseContext(AssetBundleInitFinish initCb, AssetBundleProgress progressCb)
	{
		yield return Coroutiner.StartCoroutine(_initlizeDependData(initCb, progressCb));

		s_bInitlized = true;
	}
}
