using UnityEngine;

// The persistent panel of owned skills. Pure UI container: Player owns the
// skill list; this only spawns/clears the clickable tokens.
public class SkillBar : MonoBehaviour
{
    [SerializeField] GameObject skillTokenPrefab;

    public SkillToken AddToken(SkillsSO skill)
    {
        var go = Instantiate(skillTokenPrefab, transform);
        var token = go.GetComponent<SkillToken>();
        token.Bind(skill);
        return token;
    }

    public void Clear()
    {
        foreach (var token in GetComponentsInChildren<SkillToken>())
            Destroy(token.gameObject);
    }
}
