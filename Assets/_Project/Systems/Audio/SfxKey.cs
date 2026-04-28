namespace LastPatrol.Systems.Audio
{
    /// <summary>
    /// 사운드 이벤트 키 — AudioManager.PlaySfx(SfxKey) 호출 시 SfxLibrary에서 매칭.
    /// 새 이벤트 추가 시 enum + SfxLibrary에 entry 둘 다 추가 필요.
    /// </summary>
    public enum SfxKey
    {
        // Combat
        BulletFireRobot,    // M-07 사격
        BulletFireEnemy,    // 적 사격 (휴머노이드/드론)
        BulletHit,          // 명중 임팩트
        BulletAbsorb,       // 벽·차량 흡수 (둔탁)
        AmbushAlarm,        // AMBUSH 시작 toast 동시
        CombatClear,        // 전투 종료 toast 동시
        DroneApproach,      // 드론 wave 시작 시

        // Vehicle
        VehicleIgnition,    // 시동 (Mount 시)
        VehicleDismount,    // 하차
        VehicleHijack,      // 강탈 성공
        VehicleCrash,       // 차량 충돌
        VehicleBrake,       // 급정거

        // Maren
        MarenHit,           // 데미지 받음
        MarenDeath,         // 사망
        MarenJump,
        MarenCower,         // 엄폐 진입

        // Battery / Loot
        BatteryLoot,        // 폐로봇 루팅
        BatteryCharge,      // R 키 충전 (1배터리 → +30%)
        BatteryEmpty,       // M-07 배터리 0 도달

        // UI / Flow
        DispatchBeep,       // 사건 호출
        DispatchOnScene,    // ON SCENE 도달
        SceneTransition,    // 페이드
        GameOver,           // GAME OVER UI 등장

        // Investigation (실내)
        ClueFound,          // 단서 발견
        DialogueAdvance,    // 대사 한 줄 진행
        ChoiceSelect,       // 선택지 선택
        RollDice,           // 스킬 롤
    }
}
