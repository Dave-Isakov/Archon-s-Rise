using TMPro;
using UnityEngine;
using DG.Tweening;

// Watches the four Player stats and animates each number from its old value to its
// new one (count + punch-scale + colour flash) whenever it changes. Because it only
// observes the value, it counts UP on play and DOWN on undo with no command hook.
public class StatsDisplay : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] TextMeshProUGUI attackText;
    [SerializeField] TextMeshProUGUI defendText;
    [SerializeField] TextMeshProUGUI influenceText;
    [SerializeField] TextMeshProUGUI exploreText;
    [SerializeField] TextMeshProUGUI siegeText;
    [SerializeField] Color defaultColor = Color.white;
    [SerializeField] float animTime = 0.35f;

    int _atk, _def, _inf, _exp, _sge;
    Tween _atkT, _defT, _infT, _expT, _sgeT;

    void Start()
    {
        // Seed caches and labels without animating the initial values.
        _atk = player.PlayerAttack;    attackText.text    = _atk.ToString();
        _def = player.PlayerDefend;    defendText.text    = _def.ToString();
        _inf = player.PlayerInfluence; influenceText.text = _inf.ToString();
        _exp = player.PlayerExplore;   exploreText.text   = _exp.ToString();
        _sge = player.PlayerSiege;     siegeText.text     = _sge.ToString();
    }

    void Update()
    {
        if (player.PlayerAttack    != _atk) Animate(attackText,    ref _atk, ref _atkT, player.PlayerAttack,    StatType.Attack);
        if (player.PlayerDefend    != _def) Animate(defendText,    ref _def, ref _defT, player.PlayerDefend,    StatType.Defend);
        if (player.PlayerInfluence != _inf) Animate(influenceText, ref _inf, ref _infT, player.PlayerInfluence, StatType.Influence);
        if (player.PlayerExplore   != _exp) Animate(exploreText,   ref _exp, ref _expT, player.PlayerExplore,   StatType.Explore);
        if (player.PlayerSiege     != _sge) Animate(siegeText,     ref _sge, ref _sgeT, player.PlayerSiege,     StatType.Siege);
    }

    void Animate(TextMeshProUGUI label, ref int cached, ref Tween handle, int newValue, StatType stat)
    {
        int from = cached;
        cached = newValue;                 // snap the cache immediately so we never re-trigger
        handle?.Kill();                    // kill any in-flight count for this stat

        // Count old -> new via a 0..1 progress float (avoids relying on a DOTween int plugin).
        handle = DOTween.To(() => 0f, p =>
        {
            label.text = Mathf.RoundToInt(Mathf.Lerp(from, newValue, p)).ToString();
        }, 1f, animTime).OnComplete(() => label.text = newValue.ToString());

        // Punch the number and flash it to the stat accent, then settle to default.
        label.transform.DOKill();
        label.transform.localScale = Vector3.one;
        label.transform.DOPunchScale(Vector3.one * 0.35f, animTime, 6, 0.6f);

        Color accent = StatPalette.For(stat);
        label.color = accent;
        DOTween.Kill(label);               // kill any in-flight colour tween on this label
        DOTween.To(() => 0f, f => label.color = Color.Lerp(accent, defaultColor, f), 1f, animTime)
               .SetId(label);             // id so we can guarantee a clean colour each change
    }

    // The UI transform of a stat's number, used by StatEchoes as the flight target.
    public Transform AnchorFor(StatType stat)
    {
        if (stat == StatType.Attack)    return attackText.transform;
        if (stat == StatType.Defend)    return defendText.transform;
        if (stat == StatType.Influence) return influenceText.transform;
        if (stat == StatType.Explore)   return exploreText.transform;
        if (stat == StatType.Siege)     return siegeText.transform;
        return null;
    }
}
