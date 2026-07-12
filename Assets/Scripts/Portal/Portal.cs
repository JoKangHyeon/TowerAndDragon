using UnityEngine;
using UnityEngine.Splines;

public class Portal : MonoBehaviour
{
    public PortalId Id {get;}
    public Transform SpawnPoint {get;}
      
    // 지상적일 경우 사용할 경로
    public SplineContainer GroundPath {get;}
    public Transform MainCastle {get;}
    public bool IsActive {get;}
    public bool IsConfigured {get;}

}
