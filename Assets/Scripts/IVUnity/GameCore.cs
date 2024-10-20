using UnityEngine;

public class GameCore : MonoBehaviour
{
    private static GameCore instance;
    public GameObject gameUI;
    public GameObject possesPopup;
    public GameObject playerPrefab;
    public GameObject worldCamera;
    private bool ready;
    private bool possesed;
    private GTADatLoader loader;
    private void Awake()
    {
        instance = this;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && ready && !possesed)
        {
            possesed = true;
            possesPopup.SetActive(false);
            worldCamera.SetActive(false);
            var player = Instantiate(playerPrefab, worldCamera.transform.position + worldCamera.transform.forward, Quaternion.identity);
            player.GetComponent<Player>().Load(loader);
        }

    }
    public static void SetReady(GTADatLoader loader)
    {
        instance.ready = true;
        instance.loader = loader;
        instance.gameUI.SetActive(true);
    }
}
