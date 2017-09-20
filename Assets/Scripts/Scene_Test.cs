using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Scene_Test : MonoBehaviour
{
	[SerializeField]private Button mBackButton = null;

	void Awake()
	{
	}

	void Start () 
	{
	}

	void OnEnable ()
	{
		mBackButton.onClick.AddListener(HandleBackEvent);
	}

	void OnDisable ()
	{
		mBackButton.onClick.RemoveListener(HandleBackEvent);
	}

	void OnDestroy()
	{
	}

	#region UI Event
	private void HandleBackEvent()
	{
		SceneManager.LoadSceneAsync("AssetBundle");
	}
	#endregion
}
