using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// An editor tool to automatically generate a complete Twin-Stick Shooter template.
/// This includes creating scripts, scenes, prefabs, and setting up GameObjects.
/// </summary>
public class TemplateGenerator
{
    private const string TemplateRootFolder = "Assets/TwinStickTemplate";
    private const string PrefabFolder = TemplateRootFolder + "/Prefabs";
    private const string SceneFolder = TemplateRootFolder + "/Scenes";
    private const string ScriptsFolder = TemplateRootFolder + "/Scripts";
    private const string SettingsFolder = TemplateRootFolder + "/Settings";

    [MenuItem("Tools/Create Twin-Stick Base Game")]
    public static void GenerateTemplate()
    {
        // Step 1: Create all the necessary C# script files first.
        Debug.Log("Generating C# scripts...");
        CreateDirectories();
        CreateAllScripts();
        Debug.Log("All scripts generated successfully.");

        // Step 2: Force a refresh and compile. The rest of the logic is deferred
        // until after the compilation is finished.
        Debug.Log("Forcing AssetDatabase refresh and script compilation...");
        EditorApplication.delayCall += OnScriptsCompiled;
        AssetDatabase.Refresh();
    }

    private static void OnScriptsCompiled()
    {
        // This method is called after scripts have been recompiled.
        // It's now safe to use the newly created script types.
        EditorApplication.delayCall -= OnScriptsCompiled; // Unsubscribe
        Debug.Log("Script compilation complete. Now creating assets...");

        CreateTemplateAssets();
    }

    private static void CreateTemplateAssets()
    {
        // Step 3: Create the Input Actions asset
        Debug.Log("Creating Input Actions asset...");
        InputActionAsset inputActionAsset = CreateInputActions();

        // Step 4: Create the Prefabs
        Debug.Log("Creating core gameplay prefabs...");
        GameObject playerPrefab = CreatePlayerPrefab(inputActionAsset);
        GameObject enemyPrefab = CreateEnemyPrefab();
        GameObject projectilePrefab = CreateProjectilePrefab();
        // Assign the projectile prefab to the player prefab
        playerPrefab.GetComponent<PlayerController>().projectilePrefab = projectilePrefab;
        // Save the change to the player prefab
        PrefabUtility.SavePrefabAsset(playerPrefab);
        Debug.Log("Prefabs created successfully!");

        // Step 5: Create the Scenes
        Debug.Log("Creating MainMenu and Gameplay scenes...");
        Scene mainMenuScene = CreateMainMenuScene();
        Scene gameplayScene = CreateGameplayScene(playerPrefab, enemyPrefab);
        Debug.Log("Scenes created successfully!");

        // Step 6: Add scenes to Build Settings
        Debug.Log("Adding scenes to Build Settings...");
        AddScenesToBuildSettings(mainMenuScene, gameplayScene);
        Debug.Log("Build Settings configured!");

        // Step 7: Final instructions and cleanup
        Debug.LogWarning("If you haven't, please import 'TextMeshPro Essential Resources' via the 'Window -> TextMeshPro -> Import TMP Essential Resources' menu for the UI to display correctly.");
        Debug.Log("Twin-Stick Base Game generation complete! Opening MainMenu scene.");
        EditorSceneManager.OpenScene(GetScenePath("MainMenu"));
    }

    #region Script and Folder Creation

    private static void CreateDirectories()
    {
        if (!Directory.Exists(TemplateRootFolder)) Directory.CreateDirectory(TemplateRootFolder);
        if (!Directory.Exists(PrefabFolder)) Directory.CreateDirectory(PrefabFolder);
        if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);
        if (!Directory.Exists(ScriptsFolder)) Directory.CreateDirectory(ScriptsFolder);
        if (!Directory.Exists(SettingsFolder)) Directory.CreateDirectory(SettingsFolder);
    }

    private static void CreateAllScripts()
    {
        // PlayerController.cs
        string playerControllerCode = @"
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header(""Movement"")]
    public float moveSpeed = 5f;

    [Header(""Stats"")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header(""Combat"")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector3 lookPosition;
    private Camera mainCamera;
    private UIManager uiManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // Find the UIManager on the persistent GameManager
        if (GameManager.Instance != null)
        {
            uiManager = GameManager.Instance.GetComponent<UIManager>();
        }
        if (uiManager != null)
        {
            uiManager.UpdateHealthBar(currentHealth, maxHealth);
        }
    }

    public void OnMove(InputValue value) { moveInput = value.Get<Vector2>(); }
    public void OnLook(InputValue value)
    {
        Vector2 mouseScreenPosition = value.Get<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask(""Ground"")))
        {
            lookPosition = hit.point;
        }
    }
    public void OnFire()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        }
    }

    private void FixedUpdate()
    {
        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        rb.velocity = movement * moveSpeed;
    }

    private void Update()
    {
        Vector3 direction = (lookPosition - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
        if (uiManager != null)
        {
            uiManager.UpdateCoordinates(transform.position);
        }
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (uiManager != null) uiManager.UpdateHealthBar(currentHealth, maxHealth);
        if (currentHealth <= 0) Die();
    }

    private void Die()
    {
        Debug.Log(""Player has died."");
        if (GameManager.Instance != null) GameManager.Instance.ReloadGame();
        Destroy(gameObject);
    }
}";
        File.WriteAllText(ScriptsFolder + "/PlayerController.cs", playerControllerCode);

        File.WriteAllText(ScriptsFolder + "/GameManager.cs", @"
using UnityEngine;
using UnityEngine.SceneManagement;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void StartGame() { SceneManager.LoadScene(""Gameplay""); }
    public void ReloadGame() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}");

        File.WriteAllText(ScriptsFolder + "/CameraFollow.cs", @"
using UnityEngine;
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 10f, -5f);
    public float smoothSpeed = 0.125f;
    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
        transform.LookAt(target.position);
    }
}");

        File.WriteAllText(ScriptsFolder + "/EnemyAI.cs", @"
using UnityEngine;
public class EnemyAI : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float health = 50f;
    public Transform playerTarget;
    void Update()
    {
        if (playerTarget != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, playerTarget.position, moveSpeed * Time.deltaTime);
            transform.LookAt(playerTarget);
        }
    }
    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0) Destroy(gameObject);
    }
}");

        File.WriteAllText(ScriptsFolder + "/Projectile.cs", @"
using UnityEngine;
[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    public float speed = 20f;
    public float damage = 10f;
    public float lifetime = 3f;
    void Start()
    {
        GetComponent<Rigidbody>().velocity = transform.forward * speed;
        Destroy(gameObject, lifetime);
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(""Enemy""))
        {
            other.GetComponent<EnemyAI>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}");

        File.WriteAllText(ScriptsFolder + "/UIManager.cs", @"
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class UIManager : MonoBehaviour
{
    [Header(""Menu Elements"")]
    public GameObject mainMenuPanel;

    [Header(""HUD Elements"")]
    public GameObject hudPanel;
    public Slider healthBar;
    public TextMeshProUGUI coordinatesText;

    public void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
    }
    public void UpdateCoordinates(Vector3 playerPosition)
    {
        if (coordinatesText != null)
        {
            coordinatesText.text = $""X: {playerPosition.x:F1} | Z: {playerPosition.z:F1}"";
        }
    }
}");
    }

    #endregion

    #region Asset Creation

    private static InputActionAsset CreateInputActions()
    {
        InputActionAsset asset = new InputActionAsset();
        var playerActionMap = asset.AddActionMap("Player");
        var moveAction = playerActionMap.AddAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector(mode=2)").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        playerActionMap.AddAction("Look", InputActionType.Value, "<Mouse>/position");
        playerActionMap.AddAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
        AssetDatabase.CreateAsset(asset, SettingsFolder + "/TwinStickInputActions.inputactions");
        return asset;
    }

    private static GameObject CreatePlayerPrefab(InputActionAsset inputActions)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        player.name = "Player";
        player.tag = "Player";
        player.AddComponent<PlayerController>();
        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        PlayerInput playerInput = player.AddComponent<PlayerInput>();
        playerInput.actions = inputActions;
        playerInput.defaultActionMap = "Player";
        playerInput.notificationBehavior = PlayerNotifications.SendMessages;
        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(player.transform);
        firePoint.transform.localPosition = new Vector3(0, 0, 0.7f);
        player.GetComponent<PlayerController>().firePoint = firePoint.transform;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PrefabFolder + "/Player.prefab");
        GameObject.DestroyImmediate(player);
        return prefab;
    }

    private static GameObject CreateEnemyPrefab()
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.name = "Enemy";
        enemy.tag = "Enemy";
        enemy.AddComponent<EnemyAI>();
        enemy.AddComponent<Rigidbody>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(enemy, PrefabFolder + "/Enemy.prefab");
        GameObject.DestroyImmediate(enemy);
        return prefab;
    }

    private static GameObject CreateProjectilePrefab()
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Projectile";
        projectile.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        projectile.AddComponent<Projectile>();
        Rigidbody rb = projectile.AddComponent<Rigidbody>();
        rb.useGravity = false;
        SphereCollider col = projectile.GetComponent<SphereCollider>();
        col.isTrigger = true;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(projectile, PrefabFolder + "/Projectile.prefab");
        GameObject.DestroyImmediate(projectile);
        return prefab;
    }

    #endregion

    #region Scene Creation

    private static Scene CreateMainMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "MainMenu";
        GameObject.DestroyImmediate(Camera.main.gameObject);

        GameObject gameManagerGO = new GameObject("_GameManager", typeof(GameManager), typeof(UIManager));
        UIManager uiManager = gameManagerGO.GetComponent<UIManager>();

        GameObject canvasGO = CreateUICanvas(scene);
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        GameObject menuPanel = new GameObject("MainMenuPanel", typeof(RectTransform));
        menuPanel.transform.SetParent(canvasGO.transform, false);
        uiManager.mainMenuPanel = menuPanel;

        GameObject startButton = CreateButton(menuPanel.transform, "StartButton", "Start Game", new Vector2(0, 20));
        startButton.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance.StartGame());
        GameObject quitButton = CreateButton(menuPanel.transform, "QuitButton", "Quit", new Vector2(0, -20));
        quitButton.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance.QuitGame());

        EditorSceneManager.SaveScene(scene, GetScenePath("MainMenu"));
        return scene;
    }

    private static Scene CreateGameplayScene(GameObject playerPrefab, GameObject enemyPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "Gameplay";
        CreateLayer("Ground");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10, 1, 10);
        ground.layer = LayerMask.NameToLayer("Ground");

        Camera mainCamera = Camera.main;
        mainCamera.gameObject.AddComponent<CameraFollow>();

        GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        playerInstance.transform.position = Vector3.zero;
        mainCamera.GetComponent<CameraFollow>().target = playerInstance.transform;

        GameObject enemyInstance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
        enemyInstance.transform.position = new Vector3(5, 0, 5);
        enemyInstance.GetComponent<EnemyAI>().playerTarget = playerInstance.transform;

        // Get the UIManager from the persistent GameManager
        UIManager uiManager = GameManager.Instance.GetComponent<UIManager>();

        GameObject canvasGO = CreateUICanvas(scene);
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        GameObject hudPanel = new GameObject("HUDPanel", typeof(RectTransform));
        hudPanel.transform.SetParent(canvasGO.transform, false);
        uiManager.hudPanel = hudPanel;

        uiManager.healthBar = CreateHealthBar(hudPanel.transform);
        uiManager.coordinatesText = CreateCoordinatesText(hudPanel.transform);

        // This is a bit of a hack: disable the main menu panel when in gameplay
        if(uiManager.mainMenuPanel != null) uiManager.mainMenuPanel.SetActive(false);

        EditorSceneManager.SaveScene(scene, GetScenePath("Gameplay"));
        return scene;
    }

    #endregion

    #region Build and UI Helpers

    private static void AddScenesToBuildSettings(Scene mainMenu, Scene gameplay)
    {
        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(GetScenePath("MainMenu"), true),
            new EditorBuildSettingsScene(GetScenePath("Gameplay"), true)
        };
    }

    private static GameObject CreateUICanvas(Scene scene)
    {
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        return canvasGO;
    }

    private static GameObject CreateButton(Transform parent, string name, string buttonText, Vector2 position)
    {
        GameObject buttonGO = new GameObject(name, typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);
        RectTransform rt = buttonGO.GetComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(160, 30);
        GameObject textGO = new GameObject("Text", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(buttonGO.transform, false);
        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = buttonText;
        tmp.color = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;
        return buttonGO;
    }

    private static Slider CreateHealthBar(Transform parent)
    {
        GameObject sliderGO = new GameObject("HealthBar", typeof(Slider));
        sliderGO.transform.SetParent(parent, false);
        RectTransform rt = sliderGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -20);
        rt.sizeDelta = new Vector2(200, 20);

        // Create Background
        GameObject background = new GameObject("Background", typeof(Image));
        background.transform.SetParent(sliderGO.transform, false);
        Image bgImage = background.GetComponent<Image>();
        bgImage.color = new Color32(255, 255, 255, 100);
        RectTransform bgRT = background.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        // Create Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.sizeDelta = Vector2.zero;

        // Create Fill
        GameObject fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = Color.red;
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.sizeDelta = Vector2.zero;

        // Assign references to Slider
        Slider slider = sliderGO.GetComponent<Slider>();
        slider.fillRect = fillRT;
        slider.targetGraphic = fillImage;
        slider.value = 1;

        return slider;
    }

    private static TextMeshProUGUI CreateCoordinatesText(Transform parent)
    {
        GameObject textGO = new GameObject("CoordinatesText", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(parent, false);
        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(10, 10);
        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "X: 0 | Z: 0";
        tmp.fontSize = 14;
        return tmp;
    }

    private static void CreateLayer(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return; // Layer already exists
        }
        for (int j = 8; j < layers.arraySize; j++)
        {
            SerializedProperty layerSP = layers.GetArrayElementAtIndex(j);
            if (string.IsNullOrEmpty(layerSP.stringValue))
            {
                layerSP.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"Layer '{layerName}' created.");
                return;
            }
        }
    }

    #endregion

    #region Utility

    private static string GetScenePath(string sceneName)
    {
        return SceneFolder + "/" + sceneName + ".unity";
    }

    #endregion
}
