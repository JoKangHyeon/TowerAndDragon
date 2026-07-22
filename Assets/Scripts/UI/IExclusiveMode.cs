// 한 번에 하나만 열려야 하는 UI 모드(창) 공통 인터페이스. UIManager.OpenExclusive가 조정한다.
public interface IExclusiveMode
{
    bool IsOpen { get; }
    void Open();
    void Close();
}
