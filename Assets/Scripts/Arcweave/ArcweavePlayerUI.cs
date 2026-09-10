using Arcweave.Interpreter.INodes;
using Arcweave.Project;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Arcweave
{
    /// <summary>
    /// Provides the user interface for the Arcweave narrative system
    /// </summary>
    public class ArcweavePlayerUI : MonoBehaviour
    {
        [Header("References")]
        public ArcweavePlayer player;
        public Text content;
        public RawImage cover;
        public Button buttonTemplate;
        public Button saveButton;
        public Button loadButton;
        public RawImage componentCover;

        [Header("Variables UI")]
        public Text variablesText;
        public bool showVariables = true;
        public float variableUpdateInterval = 0.5f;
        
        [Header("Animations")]
        public float crossfadeTime = 0.3f;
        public bool animateTextEntries = true;
        
        [Header("Debug Settings")]
        public bool debugMode = false;

        // Private variables
        private List<Button> tempButtons = new List<Button>();
        private bool isDialogueEndElement = false;
        private float nextVariableUpdate = 0f;
        private bool isInitialized = false;
        private Element currentElement = null;
        private Coroutine hideCoverCoroutine = null;
        private Coroutine hideComponentCoverCoroutine = null;

        void Awake()
        {
            // Ensure we have a valid player reference
            if (player == null)
            {
                player = GetComponent<ArcweavePlayer>();
                if (player == null)
                {
                    player = FindAnyObjectByType<ArcweavePlayer>();
                    if (player == null)
                    {
                        Debug.LogError("ArcweavePlayer not found. Please assign in the inspector.");
                    }
                }
            }
        }

        void OnEnable() 
        {
            Initialize();
        }
        
        /// <summary>
        /// Initialize the UI elements and event listeners
        /// </summary>
        private void Initialize()
        {
            if (isInitialized) return;
            
            // Initialize UI elements
            if (componentCover != null) componentCover.gameObject.SetActive(false);
            if (buttonTemplate != null) buttonTemplate.gameObject.SetActive(false);
            
            // Set up button listeners
            SetupButtons();

            // Subscribe to Arcweave events
            SubscribeToEvents();

            // Initialize variables display
            if (variablesText != null && showVariables)
                UpdateVariablesDisplay();
                
            isInitialized = true;
            
            if (debugMode)
            {
                Debug.Log("ArcweavePlayerUI initialized");
            }
        }
        
        /// <summary>
        /// Set up save/load button functionality
        /// </summary>
        private void SetupButtons()
        {
            if (saveButton != null) 
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(Save);
            }
            
            // Auto-activation of load button (disabled)
            // if (loadButton != null)
            // {
            //     loadButton.gameObject.SetActive(PlayerPrefs.HasKey(ArcweavePlayer.SAVE_KEY + "_currentElement"));
            // }
        }
        
        /// <summary>
        /// Subscribe to ArcweavePlayer events
        /// </summary>
        private void SubscribeToEvents()
        {
            if (player != null)
            {
                // Unsubscribe first to prevent duplicate subscriptions
                UnsubscribeFromEvents();
                
                player.onElementEnter += OnElementEnter;
                player.onElementOptions += OnElementOptions;
                player.onWaitInputNext += OnWaitInputNext;
                player.onProjectFinish += OnProjectFinish;
                
                if (debugMode)
                {
                    Debug.Log("Subscribed to ArcweavePlayer events");
                }
            }
            else
            {
                Debug.LogWarning("Cannot subscribe to ArcweavePlayer events - player reference is null");
            }
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
        }
        
        /// <summary>
        /// Unsubscribe from ArcweavePlayer events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (player != null)
            {
                player.onElementEnter -= OnElementEnter;
                player.onElementOptions -= OnElementOptions;
                player.onWaitInputNext -= OnWaitInputNext;
                player.onProjectFinish -= OnProjectFinish;
                
                if (debugMode)
                {
                    Debug.Log("Unsubscribed from ArcweavePlayer events");
                }
            }
        }

        void Update()
        {
            // Update variables display periodically
            if (showVariables && variablesText != null && Time.time >= nextVariableUpdate)
            {
                UpdateVariablesDisplay();
                nextVariableUpdate = Time.time + variableUpdateInterval;
            }
        }

        /// <summary>
        /// Updates the variable display text
        /// </summary>
        private void UpdateVariablesDisplay()
        {
            if (player?.aw?.Project == null) return;

            StringBuilder sb = new StringBuilder();
            bool hasGlobalVariables = false;
            bool hasBoardVariables = false;

            // Globals
            foreach (var variable in player.aw.Project.Variables)
            {
                if (variable == null || variable.Parent != null) continue;

                if (!hasGlobalVariables)
                {
                    sb.AppendLine("Global Variables:");
                    hasGlobalVariables = true;
                }

                sb.AppendLine($"{variable.Name}: {variable.Value}");
            }

            //Boards
            foreach (var variable in player.aw.Project.Variables)
            {
                if (variable.Parent != null)
                {
                    if (variable.Parent is Board b)
                    {
                        if (!hasBoardVariables)
                        {
                            if (sb.Length > 0) sb.AppendLine();
                            sb.AppendLine("Board Variables:");
                            hasBoardVariables = true;
                        }

                        var boardLabel = string.IsNullOrEmpty(b.Name) ? b.Id : b.Name;
                        sb.AppendLine($"{boardLabel}.{variable.Name}: {variable.Value}");
                    }
                }
            }
            variablesText.text = sb.ToString();

        }

        /// <summary>
        /// Save the current state of the narrative
        /// </summary>
        public void Save() 
        {
            if (player == null) return;
            
            player.Save();
            
            // Auto-activation of load button (disabled)
            // if (loadButton != null) 
            // {
            //     loadButton.gameObject.SetActive(true);
            // }
            
            Debug.Log("Arcweave state saved");
        }

        /// <summary>
        /// Load a saved state of the narrative
        /// </summary>
        public void Load() 
        {
            if (player == null) return;
            
            if (!PlayerPrefs.HasKey(ArcweavePlayer.SAVE_KEY + "_currentElement"))
            {
                Debug.LogWarning("No saved state found");
                return;
            }
            
            ClearTempButtons();
            player.Load();
            Debug.Log("Arcweave state loaded");
        }

        /// <summary>
        /// Handle entering a new narrative element
        /// </summary>
        private void OnElementEnter(Element element) 
        {
            if (element == null)
            {
                Debug.LogError("Cannot display null element");
                return;
            }
            
            currentElement = element;
            
            if (debugMode)
            {
                Debug.Log($"Entering element: {element.Title}");
            }
            
            // Clear any existing buttons
            ClearTempButtons();
            
            // Set up content text
            UpdateContentText(element);
            
            // Handle cover image
            HandleCoverImage(element);
            
            // Handle component cover image
            HandleComponentCoverImage(element);
            
            // Check if current element has the dialogue_end tag
            CheckForDialogueEndTag(element);
        }
        
        /// <summary>
        /// Updates the content text with element content
        /// </summary>
        private void UpdateContentText(Element element)
        {
            if (content == null) return;
            
            if (element.HasContent())
            {
                // Run content script before displaying
                element.RunContentScript();
                
                // Set the content text
                content.text = element.RuntimeContent;
                
                if (debugMode)
                {
                    Debug.Log($"Element content: {element.RuntimeContent}");
                }
            }
            else
            {
                content.text = "<i>[ No Content ]</i>";
                Debug.LogWarning($"Element '{element.Title}' has no content");
            }
            
            // Animate content fade-in
            if (animateTextEntries)
            {
                content.canvasRenderer.SetAlpha(0);
                content.CrossFadeAlpha(1f, crossfadeTime, false);
            }
            else
            {
                content.canvasRenderer.SetAlpha(1f);
            }
        }

        /// <summary>
        /// Handles the cover image display
        /// </summary>
        private void HandleCoverImage(Element element)
        {
            if (cover == null) return;
            
            var coverImage = element.GetCoverImage();
            
            if (coverImage != null) 
            {
                cover.gameObject.SetActive(true);
                cover.texture = coverImage;
                
                if (animateTextEntries)
                {
                    cover.canvasRenderer.SetAlpha(0);
                    cover.CrossFadeAlpha(1f, crossfadeTime, false);
                }
                else
                {
                    cover.canvasRenderer.SetAlpha(1f);
                }
            } 
            else 
            {
                if (cover.gameObject.activeInHierarchy)
                {
                    if (animateTextEntries)
                    {
                        cover.canvasRenderer.SetAlpha(1);
                        cover.CrossFadeAlpha(0f, crossfadeTime, false);
                        // Hide after fade animation completes
                        if (hideCoverCoroutine != null)
                        {
                            StopCoroutine(hideCoverCoroutine);
                        }
                        hideCoverCoroutine = StartCoroutine(HideCoverAfterDelay(crossfadeTime));
                    }
                    else
                    {
                        cover.gameObject.SetActive(false);
                    }
                }
            }
        }
        
        /// <summary>
        /// Coroutine to hide cover after animation delay
        /// </summary>
        private IEnumerator HideCoverAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (cover != null)
            {
                cover.gameObject.SetActive(false);
            }

            hideCoverCoroutine = null;
        }

        /// <summary>
        /// Handles the component cover image display
        /// </summary>
        private void HandleComponentCoverImage(Element element)
        {
            if (componentCover == null) return;
            
            var compImage = element.GetFirstComponentCoverImage();
            
            if (compImage != null) 
            {
                componentCover.gameObject.SetActive(true);
                componentCover.texture = compImage;
                
                if (animateTextEntries)
                {
                    componentCover.canvasRenderer.SetAlpha(0);
                    componentCover.CrossFadeAlpha(1f, crossfadeTime, false);
                }
                else
                {
                    componentCover.canvasRenderer.SetAlpha(1f);
                }
            } 
            else 
            {
                if (componentCover.gameObject.activeInHierarchy)
                {
                    if (animateTextEntries)
                    {
                        componentCover.canvasRenderer.SetAlpha(1);
                        componentCover.CrossFadeAlpha(0f, crossfadeTime, false);
                        // Hide after fade animation completes
                        if (hideComponentCoverCoroutine != null)
                        {
                            StopCoroutine(hideComponentCoverCoroutine);
                        }
                        hideComponentCoverCoroutine = StartCoroutine(HideComponentCoverAfterDelay(crossfadeTime));
                    }
                    else
                    {
                        componentCover.gameObject.SetActive(false);
                    }
                }
            }
        }
        
        /// <summary>
        /// Coroutine to hide component cover after animation delay
        /// </summary>
        private IEnumerator HideComponentCoverAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (componentCover != null)
            {
                componentCover.gameObject.SetActive(false);
            }

            hideComponentCoverCoroutine = null;
        }

        /// <summary>
        /// Checks if the current element has a dialogue end tag
        /// </summary>
        private void CheckForDialogueEndTag(Element element)
        {
            isDialogueEndElement = false;
            
            if (GameManager.Instance == null) return;
            
            DialogueTrigger activeTrigger = GameManager.Instance.GetActiveDialogueTrigger();
            if (activeTrigger != null)
            {
                isDialogueEndElement = GameManager.Instance.HasDialogueEndTag(element);
                
                if (isDialogueEndElement && debugMode)
                {
                    Debug.Log($"Element '{element.Title}' has dialogue_end tag");
                }
            }
        }

        /// <summary>
        /// Handle displaying options to the player
        /// </summary>
        private void OnElementOptions(Options options, System.Action<int> callback) 
        {
            if (options == null || options.Paths == null || options.Paths.Count == 0) 
            {
                Debug.LogWarning("No options provided to display");
                return;
            }
            
            for (int i = 0; i < options.Paths.Count; i++) 
            {
                int index = i; // Create stable copy for the delegate
                string text = !string.IsNullOrEmpty(options.Paths[i].label) ? 
                              options.Paths[i].label : 
                              "<i>[ No Label ]</i>";
                
                Button button;
                if (isDialogueEndElement) 
                {
                    // For dialogue_end elements, create button that ends dialogue after selection
                    button = MakeButton(text, () => {
                        // First end dialogue immediately
                        EndCurrentDialogue();
                        // Then process the callback
                        callback(index);
                    });
                } 
                else 
                {
                    // Normal behavior for non-ending elements
                    button = MakeButton(text, () => callback(index));
                }
                
                // Position the button
                PositionButton(button, i, options.Paths.Count);
            }
        }
        
        /// <summary>
        /// Position a button in the options list
        /// </summary>
        private void PositionButton(Button button, int index, int totalOptions)
        {
            if (buttonTemplate == null || button == null) return;
            
            var buttonRect = buttonTemplate.GetComponent<RectTransform>();
            if (buttonRect == null) return;
            
            var pos = button.transform.position;
            pos.y += buttonRect.rect.height * (totalOptions - 1 - index);
            button.transform.position = pos;
        }
        
        /// <summary>
        /// End the current dialogue
        /// </summary>
        private void EndCurrentDialogue() 
        {
            var activeTrigger = GameManager.Instance?.GetActiveDialogueTrigger();
            if (activeTrigger != null) 
            {
                activeTrigger.EndDialogue();
            }
        }

        /// <summary>
        /// Handle waiting for input to proceed to next element
        /// </summary>
        private void OnWaitInputNext(System.Action next)
        {
            if (next == null) return;

            // Create a "Continue" button
            Button button;
            if (isDialogueEndElement)
            {
                // For dialogue_end elements, end dialogue before proceeding
                button = MakeButton("Continue", () => {
                    EndCurrentDialogue();
                    next();
                });
            }
            else
            {
                button = MakeButton("Continue", next);
            }
            
            // Position the button
            if (button != null && buttonTemplate != null) 
            {
                var buttonRect = buttonTemplate.GetComponent<RectTransform>();
                if (buttonRect != null) 
                {
                    button.transform.position = buttonTemplate.transform.position;
                }
            }
        }

        /// <summary>
        /// Handle project finish event
        /// </summary>
        private void OnProjectFinish(Project.Project project) 
        {
            if (debugMode)
            {
                Debug.Log("Project finished");
            }
            
            ClearTempButtons();
        }

        /// <summary>
        /// Create a button with the specified text and callback
        /// </summary>
        private Button MakeButton(string text, System.Action callback) 
        {
            if (buttonTemplate == null) 
            {
                Debug.LogError("Button template is not assigned");
                return null;
            }
            
            var button = Instantiate(buttonTemplate, buttonTemplate.transform.parent);
            button.gameObject.SetActive(true);
            
            // Set button text
            var buttonText = button.GetComponentInChildren<Text>();
            if (buttonText != null) 
            {
                buttonText.text = text;
            }
            
            // Set button callback
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback());
            
            // Add to temp buttons list for cleanup
            tempButtons.Add(button);
            
            // Animate button fade-in
            if (animateTextEntries)
            {
                var canvasGroup = button.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
                }
                
                canvasGroup.alpha = 0f;
                StartCoroutine(FadeInButton(canvasGroup));
            }
            
            return button;
        }
        
        /// <summary>
        /// Coroutine to fade in a button
        /// </summary>
        private IEnumerator FadeInButton(CanvasGroup canvasGroup)
        {
            float elapsedTime = 0f;

            while (elapsedTime < crossfadeTime)
            {
                if (canvasGroup == null) yield break;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / crossfadeTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Clear all temporary buttons
        /// </summary>
        public void ClearTempButtons() 
        {
            foreach (var button in tempButtons) 
            {
                if (button != null) 
                {
                    Destroy(button.gameObject);
                }
            }
            
            tempButtons.Clear();
        }
        
        /// <summary>
        /// Refresh the current element display
        /// </summary>
        public void RefreshCurrentElement()
        {
            if (currentElement != null)
            {
                OnElementEnter(currentElement);
            }
        }
    }
}