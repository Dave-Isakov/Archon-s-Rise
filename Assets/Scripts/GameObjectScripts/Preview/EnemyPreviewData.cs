// One previewed enemy plus its doom-scaling bonuses (zero for guardians).
// Keeps the preview honest: it must show the numbers the fight will use.
public readonly struct EnemyPreviewData
{
    public readonly EnemiesSO enemy;
    public readonly int bonusAttack;
    public readonly int bonusHP;

    public EnemyPreviewData(EnemiesSO enemy, int bonusAttack, int bonusHP)
    {
        this.enemy = enemy;
        this.bonusAttack = bonusAttack;
        this.bonusHP = bonusHP;
    }
}
