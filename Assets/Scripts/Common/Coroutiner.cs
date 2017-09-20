using UnityEngine;
using System.Collections;

/// <summary>
/// Author: 		Sebastiaan Fehr (Seb@TheBinaryMill.com)
/// Date: 			March 2013
/// Summary:		Creates MonoBehaviour instance through which 
///                 static classes can call StartCoroutine.
/// Description:    Classes that do not inherit from MonoBehaviour, or static 
///                 functions within MonoBehaviours are inertly unable to 
///                 call StartCoroutene, as this function is not static and 
///                 does not exist on Object. This Class creates a proxy though
///                 which StartCoroutene can be called, and destroys it when 
///                 no longer needed.
/// </summary>
public class Coroutiner
{
    static GameObject s_sCoroutineObject = null;
    static CoroutinerInstance s_coroutinerInstance = null;
    public static Coroutine StartCoroutine(IEnumerator iterationResult) 
	{
        if(s_sCoroutineObject == null)
        {
            s_sCoroutineObject = new GameObject("Coroutiner");
            Object.DontDestroyOnLoad(s_sCoroutineObject);
            s_coroutinerInstance = s_sCoroutineObject.AddComponent(typeof(CoroutinerInstance)) as CoroutinerInstance;
        }

        return s_coroutinerInstance.ProcessWork(iterationResult);
    }

    public static void ClearCoroutine()
    {
        if(s_sCoroutineObject != null)
        {
            Object.Destroy(s_sCoroutineObject);
            s_sCoroutineObject = null;
        }
    }

	public static void StopCoroutine(IEnumerator iterationResult)
	{
		s_coroutinerInstance.StopWork(iterationResult);
	}
}

public class CoroutinerInstance : MonoBehaviour 
{

    public Coroutine ProcessWork(IEnumerator iterationResult) 
	{
        return StartCoroutine(DestroyWhenComplete(iterationResult));
    }

    public IEnumerator DestroyWhenComplete(IEnumerator iterationResult) 
	{
        yield return StartCoroutine(iterationResult);
    }

	public void StopWork(IEnumerator iterationResult)
	{
		StopCoroutine(iterationResult);
	}

    void OnDestroy()
    {
        //Debug.LogError("XXX");
    }

    void OnDisable()
    {
		//Debug.Log ("Coroutiner Disabled---");
	}
}


public class CoroutineWrapper<T>
{
    public class CoroutineResult
    {
        public T result;
        public string strErrMsg;
    };

    public delegate void CoroutineFinProc(CoroutineResult retObj);
    public static IEnumerator wrapperCoroutine(IEnumerator co, CoroutineFinProc finishDelegate)
    {
        while (true)
        {
            System.Object current = null;

            try
            {
                if (!co.MoveNext())
                {
                    if (finishDelegate != null)
                    {
                        CoroutineResult result = new CoroutineResult();
                        finishDelegate(result);
                    }
                    yield break;
                }

                current = co.Current;
                if ((current != null) && (current.GetType() == typeof(CoroutineResult)))
                {
                    CoroutineResult result = (CoroutineResult)current;
                    if (finishDelegate != null) finishDelegate(result);

                    yield break;
                }
            }
            catch (System.Exception e)
            {
                if (finishDelegate != null)
                {
                    CoroutineResult result = new CoroutineResult();
                    result.strErrMsg = e.Message;
                    Debug.LogError("Coroutine Exceprion:" + e);
                    finishDelegate(result);
                }
                yield break;
            }

            yield return current;
        }
    }
};
