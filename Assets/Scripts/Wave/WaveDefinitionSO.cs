using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "TowerAndDragon/Wave/Wave Definition", fileName = "WaveDefinition")]
public class WaveDefinitionSO : MonoBehaviour
{
    [SerializeField] private List<PortalWaveData> _portalWaves;

    public IReadOnlyList<PortalWaveData> PortalWaves => _portalWaves;  

    // 추가할 내용 = 일차, 주기, 보스 여부, 난이도?, 자동 활성화 포탈 수    
    // 현재는 실행 가능한 웨이브 편성 단계에 맞게   
}
