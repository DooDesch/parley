using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    // Lucide icons, rendered as white PNGs under Resources/Parley/Icons. USS tints them through
    // -unity-background-image-tint-color, so one texture serves both editor skins and every state color.
    internal static class ParleyIcons
    {
        public const string IconClass = "pl-icon";

        private const string Folder = "Parley/Icons/";
        private const string ButtonIconClass = "pl-button__icon";
        private const string ButtonLabelClass = "pl-button__label";
        private const long SpinStepMs = 50;
        private const float SpinDegreesPerStep = 22.5f;

        private static readonly Dictionary<ParleyIcon, Texture2D> _textures = new ();
        private static readonly HashSet<ParleyIcon> _missing = new ();

        public static Texture2D Get(ParleyIcon icon)
        {
            if (_textures.TryGetValue(icon, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(Folder + FileName(icon));
            if (texture == null)
            {
                if (_missing.Add(icon))
                {
                    Debug.LogWarning("[Parley] Icon texture not found: Resources/" + Folder + FileName(icon) + ".png. The icon stays empty.");
                }

                return null;
            }

            _textures[icon] = texture;
            return texture;
        }

        public static VisualElement Create(ParleyIcon icon, string className = null)
        {
            VisualElement element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(IconClass);
            if (className != null)
            {
                element.AddToClassList(className);
            }

            Set(element, icon);
            return element;
        }

        public static void Set(VisualElement element, ParleyIcon icon)
        {
            Texture2D texture = Get(icon);
            element.style.backgroundImage = texture == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(texture);
        }

        public static Button IconButton(ParleyIcon icon, string text, Action onClick, string className = null)
        {
            Button button = new Button(onClick) { text = string.Empty };
            button.AddToClassList("pl-button");
            button.AddToClassList("pl-button--icon");
            if (className != null)
            {
                button.AddToClassList(className);
            }

            button.Add(Create(icon, ButtonIconClass));
            Label label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(ButtonLabelClass);
            button.Add(label);
            SetButton(button, icon, text);
            return button;
        }

        public static void SetButton(Button button, ParleyIcon icon, string text)
        {
            VisualElement image = button.Q(className: ButtonIconClass);
            if (image != null)
            {
                Set(image, icon);
            }

            Label label = button.Q<Label>(className: ButtonLabelClass);
            if (label != null)
            {
                label.text = text ?? string.Empty;
                ParleyStyles.SetVisible(label, !string.IsNullOrEmpty(text));
            }

            button.EnableInClassList("pl-button--icon-only", string.IsNullOrEmpty(text));
        }

        // Turns the icon while the work it stands for is running. The scheduled item pauses itself while the
        // element is detached, so finished or hidden cards cost nothing.
        public static void SetSpinning(VisualElement element, bool spinning)
        {
            if (!(element.userData is Spinner spinner))
            {
                if (!spinning)
                {
                    return;
                }

                spinner = new Spinner(element);
                element.userData = spinner;
            }

            spinner.SetActive(spinning);
        }

        private static string FileName(ParleyIcon icon)
        {
            string name = icon.ToString();
            StringBuilder builder = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                if (char.IsUpper(character) && i > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }

        private sealed class Spinner
        {
            private readonly VisualElement _element;
            private readonly IVisualElementScheduledItem _item;
            private float _angle;

            public Spinner(VisualElement element)
            {
                _element = element;
                _item = element.schedule.Execute(Step).Every(SpinStepMs);
                _item.Pause();
            }

            public void SetActive(bool active)
            {
                if (active)
                {
                    _item.Resume();
                    return;
                }

                _item.Pause();
                _angle = 0.0f;
                _element.style.rotate = new StyleRotate(StyleKeyword.Null);
            }

            private void Step()
            {
                _angle = (_angle + SpinDegreesPerStep) % 360.0f;
                _element.style.rotate = new Rotate(new Angle(_angle, AngleUnit.Degree));
            }
        }
    }
}
