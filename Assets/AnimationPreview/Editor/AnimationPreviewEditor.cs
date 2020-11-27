using UnityEditor;
using UnityEngine;

namespace DeveloperTools.AnimationPreview
{
    /// <summary>
    /// Animation preview editor which allows you to select clips of an Animator and play them inside the Unity Editor.
    /// </summary>
    [ExecuteInEditMode]
    [CustomEditor(typeof(AnimationPreview))]
    public class AnimationPreviewEditor : Editor
    {
#if UNITY_EDITOR
        AnimationPreviewEditor editor;
        AnimationPreview editorTarget;

        SerializedProperty clipIndex;
        SerializedProperty clipName;
        SerializedProperty animator;

        private AnimationClip previewClip;
        private bool isPlaying = false;

        int frameSlider = 0;
        int totalframes = 1;
        int currentFrame = 1;

        public void OnEnable()
        {
            editor = this;
            editorTarget = (AnimationPreview)target;

            clipIndex = serializedObject.FindProperty("clipIndex");
            clipName = serializedObject.FindProperty("clipName");
            animator = serializedObject.FindProperty("animator");

            // try to get the animator from the gameobject if none is specified
            if ( !editorTarget.animator)
            {
                editorTarget.animator = editorTarget.GetComponent<Animator>();
            }

            UpdateClipName();
        }

        public void OnDisable()
        {
            EditorApplication.update -= DoPreview;
        }

        #region Inspector
        public override void OnInspectorGUI()
        {
            editor.serializedObject.Update();

            bool animatorChanged = false;

            // help
            EditorGUILayout.HelpBox( "Play animator clips inside the Unity editor.\nPress Play or the clip button to play the selected animation.\nPress Stop to stop continuous playing.", MessageType.Info);
            
            // data
            EditorGUILayout.BeginVertical( "");
            {
                EditorGUILayout.LabelField("Clip Data", GUIStyles.BoxTitleStyle);

                EditorGUI.BeginChangeCheck();
                {
                    GUI.backgroundColor = editorTarget.animator == null ? GUIStyles.ErrorBackgroundColor : GUIStyles.DefaultBackgroundColor;
                    {
                        EditorGUILayout.PropertyField(animator);

                    }
                    GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;
                }

                // stop clip in case the animator changes
                if (EditorGUI.EndChangeCheck())
                {
                    animatorChanged = true;
                }

                GUI.enabled = false;
                EditorGUILayout.PropertyField(clipIndex);
                EditorGUILayout.PropertyField(clipName);
                GUI.enabled = true;

            }
            EditorGUILayout.EndVertical();

            // control
            EditorGUILayout.BeginVertical( "box");
            {
                EditorGUILayout.LabelField("Control", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Previous"))
                    {
                        PreviousClip();
                    }
                    if (GUILayout.Button("Next"))
                    {
                        NextClip();
                    }
                    
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    {   // Play button has special background color handling
                        GUI.backgroundColor = isPlaying ? GUIStyles.PlayBackgroundColor : GUIStyles.DefaultBackgroundColor;
                        if (GUILayout.Button("Play"))
                        {
                            PlayButtonClip();
                        }
                        GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;
                    }
                    if (GUILayout.Button("Pause"))
                    {
                        PauseButtonClip();
                    }
                    if (GUILayout.Button("Reset"))
                    {
                        ResetClip();
                    }
                    if (GUILayout.Button("Stop"))
                    {
                        StopClip();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    // Start a code block to check for GUI changes
                    EditorGUI.BeginChangeCheck();
                    frameSlider = EditorGUILayout.IntSlider(frameSlider, 0, totalframes);

                    // End the code block and update the label if a change occurred
                    if (EditorGUI.EndChangeCheck())
                    {
                        SetAnimFrame();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // clip list
            GUILayout.BeginVertical("");
            {
                EditorGUILayout.LabelField("Clip List", GUIStyles.BoxTitleStyle);

                if (editorTarget.animator && editorTarget.animator.runtimeAnimatorController)
                {
                    AnimationClip[] clips = editorTarget.animator.runtimeAnimatorController.animationClips;
                    for (int i = 0; i < clips.Length; i++)
                    {
                        AnimationClip clip = clips[i];

                        bool isCurrentClip = i == editorTarget.clipIndex;

                        GUI.backgroundColor = isCurrentClip ? GUIStyles.SelectedClipBackgroundColor : GUIStyles.DefaultBackgroundColor;
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUILayout.PrefixLabel("Clip: " + i);

                                if (GUILayout.Button(clip.name))
                                {
                                    SetClip(i);
                                    PlayClip();
                                }

                                if (GUILayout.Button(EditorGUIUtility.IconContent("AnimationClip Icon", "Open Clip in Project"), GUIStyles.ToolbarButtonStyle))
                                {
                                    OpenClip( i);
                                }

                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;
                    }
                }

            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Log Clips"))
                    {
                        LogClips();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            editor.serializedObject.ApplyModifiedProperties();

            if (animatorChanged)
            {
                StopClip();

                // reset the 
                // set index to either -1 or the first index depending on the number of animations
                editorTarget.clipIndex = editorTarget.animator == null || editorTarget.animator.runtimeAnimatorController == null || editorTarget.animator.runtimeAnimatorController.animationClips.Length == 0 ? -1 : 0;

                UpdateClipName();

                EditorUtility.SetDirty(target);

            }
        }
        #endregion Inspector        

        #region Clip Navigation
        private void PreviousClip()
        {
            editorTarget.clipIndex--;
            editorTarget.clipIndex = GetValidClipIndex(editorTarget.clipIndex);

            ClipChanged();
        }

        private void NextClip()
        {
            editorTarget.clipIndex++;
            editorTarget.clipIndex = GetValidClipIndex(editorTarget.clipIndex);
            ClipChanged();
        }

        private void SetClip( int clipIndex)
        {
            editorTarget.clipIndex = GetValidClipIndex(clipIndex);
            ClipChanged();

        }

        /// <summary>
        /// Open the clip file in project view
        /// </summary>
        /// <param name="clipIndex"></param>
        private void OpenClip(int clipIndex)
        {

            AnimationClip clip = GetClip(clipIndex);

            if (!clip)
                return;

            Selection.activeObject = clip;

        }

        private void ClipChanged()
        {
            if (isPlaying)
                PlayClip();
            else
                ResetClip();

            UpdateClipName();
        }

        private void UpdateClipName()
        {
            AnimationClip clip = GetClipToPreview();

            editorTarget.clipName = clip == null ? "" : clip.name;

        }

        private int GetValidClipIndex( int clipIndex)
        {
            if (!editorTarget.animator)
                return -1;

            int clipCount = editorTarget.animator.runtimeAnimatorController.animationClips.Length;

            // check if there are clips at all
            if (clipCount == 0)
            {
                return -1;
            }

            if (clipIndex < 0)
            {
                return clipCount - 1;
            }

            if( clipIndex >= clipCount)
            {
                return 0;
            }

            return clipIndex;

        }

        private AnimationClip GetClipToPreview()
        {
            int clipIndex = editorTarget.clipIndex;
            if (clipIndex == -1)
                return null;

            return GetClip(clipIndex);
        }

        private AnimationClip GetClip( int clipIndex)
        {
            AnimationClip[] clips = editorTarget.animator.runtimeAnimatorController.animationClips;

            if (clipIndex >= clips.Length)
                return null;

            AnimationClip clip = clips[clipIndex];

            return clip;
        }

        #endregion Clip Navigation

        #region Clip Control
        private void PlayClip()
        {
            isPlaying = true;

            previewClip = GetClipToPreview();
            ResetClip();

            EditorApplication.update -= DoPreview;
            EditorApplication.update += DoPreview;
        }


        void DoPreview()
        {
            if (!previewClip)
                return;

            currentFrame = (int)(previewClip.length *
                (editorTarget.animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1) * previewClip.frameRate);
            totalframes = (int)(previewClip.length * previewClip.frameRate);
            
            if (currentFrame >= totalframes - 1)
            {
                ResetClip();
            }
            if (isPlaying)
            {
                previewClip.SampleAnimation(editorTarget.gameObject, Time.deltaTime);
            
                editorTarget.animator.Update(Time.deltaTime);
            }
        }

        private void PlayButtonClip()
        {
            isPlaying = true;

            previewClip = GetClipToPreview();

            EditorApplication.update -= DoPreview;
            EditorApplication.update += DoPreview;
        }

        private void PauseButtonClip()
        {
            if (!previewClip)
                return;
            frameSlider = currentFrame;
            isPlaying = false;

        }

        private void SetAnimFrame()
        {
            if (!previewClip)
                return;

            isPlaying = false;
            float normalizedTime;
            if (frameSlider == 0)
            {
                normalizedTime = 0;
            }
            else
            {
                normalizedTime = (float)frameSlider / (float)totalframes;
            }         
            editorTarget.animator.Play(previewClip.name,0, normalizedTime);
            editorTarget.animator.Update(Time.deltaTime);
        }

        private void ResetClip()
        {
            if (!previewClip)
                return;
            frameSlider = 0;
            previewClip.SampleAnimation(editorTarget.gameObject, 0);

            Animator animator = editorTarget.animator;
            animator.Play(previewClip.name, 0, 0f);
            animator.Update(0);

        }

        private void StopClip()
        {
            ResetClip();
            EditorApplication.update -= DoPreview;

            isPlaying = false;

        }
        #endregion Clip Control

        #region Logging
        private void LogClips()
        {
            if (!editorTarget.animator)
                return;

            AnimationClip[] clips = editorTarget.animator.runtimeAnimatorController.animationClips;

            string text = "Clips of " + editorTarget.animator.name + ": " + clips.Length + "\n";

            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];

                text += string.Format("{0}: {1}\n", i, clip.name);
            }

            Debug.Log(text);

        }
        #endregion Logging

#endif // UNITY_EDITOR
    }

    #region Styles
    public class GUIStyles
    {
        private static GUIStyle _boxTitleStyle;
        public static GUIStyle BoxTitleStyle
        {
            get
            {
                if (_boxTitleStyle == null)
                {
                    _boxTitleStyle = new GUIStyle("Label");
                    _boxTitleStyle.fontStyle = FontStyle.BoldAndItalic;
                }
                return _boxTitleStyle;
            }
        }

        private static GUIStyle _groupTitleStyle;
        public static GUIStyle GroupTitleStyle
        {
            get
            {
                if (_groupTitleStyle == null)
                {
                    _groupTitleStyle = new GUIStyle("Label");
                    _groupTitleStyle.fontStyle = FontStyle.Bold;
                }
                return _groupTitleStyle;
            }
        }

        private static GUIStyle _toolbarButtonStyle;
        public static GUIStyle ToolbarButtonStyle
        {
            get
            {
                if (_toolbarButtonStyle == null)
                {
                    _toolbarButtonStyle = new GUIStyle("Button");
                    _toolbarButtonStyle.fixedWidth = 32f;
                    _toolbarButtonStyle.fixedHeight = EditorGUIUtility.singleLineHeight + 1;

                }
                return _toolbarButtonStyle;
            }
        }

        public static Color DefaultBackgroundColor = GUI.backgroundColor;
        public static Color ErrorBackgroundColor = new Color(1f, 0f, 0f, 1f); // red
        public static Color PlayBackgroundColor = new Color(0f, 1f, 0f, 1f); // green
        public static Color SelectedClipBackgroundColor = new Color(1f, 1f, 0f, 1f); // yellow

    }
    #endregion Styles
}

