using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Download : MonoBehaviour 
{
	[SerializeField]private Button mDownloadButton = null;
	[SerializeField]private Text mFileCountText = null;
	[SerializeField]private Text mFileSizeText = null;

	[SerializeField]private GameObject mTestRootObj = null;
	[SerializeField]private Button[] mTestButtonArray = null;
	[SerializeField]private Text mContentLabel = null;
	[SerializeField]private Image mContentImage = null;

	[SerializeField]private AudioSource mAudioSource = null;
	[SerializeField]private GameObject mRootObj = null;

	private string mStrPreUrlDirectory = "http://127.0.0.1/yungangDownload";
	private string mStrRealUrlDirectory = "http://127.0.0.1/yungangDownload";

	void Awake()
	{
	}

	void Start () 
	{
		mTestRootObj.SetActive(false);
		mContentLabel.gameObject.SetActive(false);
		mContentImage.gameObject.SetActive(false);
		mRootObj.SetActive(false);
	}

	void OnEnable ()
	{
		mDownloadButton.onClick.AddListener(HandleDownloadEvent);

		for(int i = 0; i < mTestButtonArray.Length; i++)
			EventTriggerListener.Get(mTestButtonArray[i].gameObject).onClick += HandleTestButtonEvent;
	}

	void OnDisable ()
	{
		mDownloadButton.onClick.RemoveListener(HandleDownloadEvent);

		for(int i = 0; i < mTestButtonArray.Length; i++)
			EventTriggerListener.Get(mTestButtonArray[i].gameObject).onClick -= HandleTestButtonEvent;
	}

	void OnDestroy()
	{
		mDownloadButton = null;
		mFileCountText = null;
		mFileSizeText = null;
		mTestRootObj = null;
		for(int i = 0; i < mTestButtonArray.Length; i++)
			mTestButtonArray[i] = null;
		mTestButtonArray = null;
		mContentLabel = null;
		mContentImage = null;
		mAudioSource = null;
		mRootObj = null;
	}

	IEnumerator LoadSceneAsync(string loadSceneName, AssetBundle depBundle)
	{
		AsyncOperation async = SceneManager.LoadSceneAsync(loadSceneName, LoadSceneMode.Single);
		yield return async;
	}

	#region UI Event
	private void HandleDownloadEvent()
	{
		mFileCountText.gameObject.SetActive(true);
		mFileSizeText.gameObject.SetActive(true);
		mTestRootObj.SetActive(false);
		mRootObj.SetActive(false);

		AssetBundleManager.InitlizeAssetBundle (mStrPreUrlDirectory, mStrRealUrlDirectory, OnAssetBundleInitFinish, OnAssetBundleProgress);
	}

	private void HandleTestButtonEvent(GameObject obj)
	{
		if(obj.name.Equals("0"))
		{
			mRootObj.SetActive(false);
			mContentImage.gameObject.SetActive(false);
			mContentLabel.gameObject.SetActive(true);
			TextAsset textAsset = AssetBundleManager.GetAssetObject(enResType.enResType_Bytes, "bytes_test") as TextAsset;
			if(textAsset != null)
			{
				mContentLabel.text = textAsset.text;
			}
			else
			{
				mContentLabel.text = "资源加载失败！";
			}
		}
		else if(obj.name.Equals("1"))
		{
			mRootObj.SetActive(true);
			mContentImage.gameObject.SetActive(false);
			mContentLabel.gameObject.SetActive(false);

			GameObject prefab_obj = AssetBundleManager.GetAssetObject(enResType.enResType_Prefab, "prefab_test") as GameObject;
			GameObject instantiate_obj =  GameObject.Instantiate(prefab_obj);
			instantiate_obj.transform.parent = mRootObj.transform;
			instantiate_obj.transform.position = Vector3.zero;
			instantiate_obj.transform.localScale = new Vector3(1f, 1f, 1f);
		}
		else if(obj.name.Equals("2"))
		{
			string strSceneID = "scene_test";
			AssetBundle depBundle = (AssetBundle)AssetBundleManager.GetAssetObject(enResType.enResType_Scene, strSceneID);
			StartCoroutine(LoadSceneAsync(strSceneID, depBundle));
		}
		else if(obj.name.Equals("3"))
		{
			AudioClip audioClip = AssetBundleManager.GetAssetObject(enResType.enResType_Sound, "mp3_test") as AudioClip;
			mAudioSource.PlayOneShot(audioClip);
		}
		else if(obj.name.Equals("4"))
		{
			mRootObj.SetActive(false);
			mContentLabel.gameObject.SetActive(false);
			mContentImage.gameObject.SetActive(true);

			Texture2D texture2D = AssetBundleManager.GetAssetObject(enResType.enResType_Texture, "png_test") as Texture2D;
			mContentImage.sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), Vector2.zero);
		}
	}
	#endregion

	#region Download Callback
	protected void OnAssetBundleInitFinish(bool bOK, string strErrMsg)
	{
		if(bOK)
		{
			mFileCountText.gameObject.SetActive(false);
			mFileSizeText.gameObject.SetActive(false);
			mTestRootObj.SetActive(true);
		}
		else
		{
			mFileCountText.gameObject.SetActive(false);
			mFileSizeText.gameObject.SetActive(true);

			mFileSizeText.text = strErrMsg;
		}
	}

	protected void OnAssetBundleProgress(DownLoadProgress downLoadProgress)
	{
		mFileCountText.text = "File Count : " + downLoadProgress.iFileIndex + "/" + downLoadProgress.iTotalFileCount;

		if(downLoadProgress.bCompressing)
			mFileSizeText.text = "Compressing...";
		else
			mFileSizeText.text = "File Size : " + downLoadProgress.fDownLoadBytes + "/" + downLoadProgress.fTotalBytes + " ==> " + downLoadProgress.fProgress;
	}
	#endregion
}
