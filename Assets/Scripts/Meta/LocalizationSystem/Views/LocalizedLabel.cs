using Meta.LocalizationSystem.Services;
using TMPro;
using UnityEngine;

namespace Meta.LocalizationSystem.Views
{
    /// <summary>A label with the string of a key; follows the language.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        [SerializeField] private string key;

        private TMP_Text _label;

        private void Awake() => _label = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            Loc.Changed += Draw;
            Draw();
        }

        private void OnDisable() => Loc.Changed -= Draw;

        private void Draw() => _label.text = Loc.Get(key);
    }
}
