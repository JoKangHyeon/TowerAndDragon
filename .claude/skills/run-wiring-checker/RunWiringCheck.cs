// Coplay MCP execute_script 진입점. 프로젝트 안의 WiringCheckerRunner를 호출하기만 한다.
// 이 파일은 Assets/ 밖에 있어 Unity가 컴파일하지 않고, execute_script가 그때그때 컴파일해 실행한다.
// 주의: `using System;`을 넣지 말 것 - 컴파일 실패 시 Roslyn 리소스 에러로 뭉개져 원인을 못 읽는다.
public static class RunWiringCheck
{
    public static string Execute()
    {
        return WiringCheckerRunner.RunBuildSceneCheck();
    }
}
