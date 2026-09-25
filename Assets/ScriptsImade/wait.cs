using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


public class wait : MonoBehaviour
{
  
    
    public float wait_time = 12;
    public GameObject Introobject;
    public GameObject MenuObject;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(WaitForIntro());

    }
    IEnumerator WaitForIntro()
    {
        yield return new WaitForSeconds(wait_time);
        Introobject.SetActive(false); 
        MenuObject.SetActive(true);
    }
  
}
