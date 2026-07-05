using TMPro;
using UnityEngine;

// One enemy's stat block inside the preview panel. Renders name / Attack / HP /
// Influence-cost using the same sprite-tag format as EnemyCard so the preview
// matches the combat card. Instantiated once per previewed enemy. No art field
// exists on the data model, so there is no art element here.
public class EnemyPreviewEntry : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI enemyName;
    [SerializeField] TextMeshProUGUI enemyAttack;
    [SerializeField] TextMeshProUGUI enemyHP;
    [SerializeField] TextMeshProUGUI enemyInfluence;

    public void Populate(EnemiesSO enemy)
    {
        enemyName.text = enemy.cardName;
        enemyAttack.text = "<sprite=\"Sword\" index=0> \n" + enemy.enemyAttack.ToString();
        enemyHP.text = "<sprite=\"shield\" index=0> \n" + enemy.enemyHP.ToString();
        if (enemy.canInfluence)
        {
            enemyInfluence.gameObject.SetActive(true);
            enemyInfluence.text = "<sprite=\"gem\" index=0> \n" + enemy.influenceCost.ToString();
        }
        else
        {
            enemyInfluence.gameObject.SetActive(false);
        }
    }
}
