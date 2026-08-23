using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 상태·명중 연출용 변형을 만든다. 원본 팩(Eric VFX Studio)은 3D 지면 위에서 터지는 것을 전제로
// 만들어져 있어, 정사영 2D 카메라(회전 0, z=-10)를 쓰는 이 프로젝트에서는 그대로 쓸 수 없다.
//
// 원본은 건드리지 않고 사본만 고친다.
//
// 일회성이냐 지속이냐를 먼저 볼 것. 이 팩의 Crack 계열은 보이는 자식이 전부 loop=0에
// 버스트 방출이라 한 번 터지고 끝난다(= 명중 연출). 반대로 FX_Dust_Fire만 loop=1에 연속 방출이라
// 상태가 걸려 있는 동안 계속 돌릴 수 있다(= 지속 상태 표시). 성격에 맞지 않는 자리에 넣으면
// 일회성은 중간에 끊기고 지속형은 스스로 꺼지지 않는다.
public static class BuildStatusVfx
{
    private const string SOURCE_FOLDER =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    private const string OUTPUT_FOLDER = "Assets/Imported/Prefabs/Effects/Status";
    private const string SORTING_LAYER = "Projectile";

    // <b>산출물이 결과물이다. 이 스크립트는 그것을 만들어 낸 발판일 뿐이다.</b>
    //
    // 출력 폴더 Assets/Imported는 버려지는 곳이 아니라 팀이 공유하는 별도 private 저장소다
    // (github.com/JoKangHyeon/TowerAndDragon_Imported, 메인 저장소에서만 .gitignore로 빠져 있다).
    // 여기에 커밋한 프리팹은 팀원 전원이 그대로 받아 쓴다 - 각자 이 생성기를 돌리지 않는다.
    //
    // 그래서 Build는 이미 있는 산출물을 건너뛴다. 손으로 다듬은 값이 얹혀 있을 수 있고,
    // 아직 커밋하지 않았다면 덮어쓰는 순간 사라지기 때문이다(커밋했다면
    // git -C Assets/Imported checkout으로 되돌릴 수 있다).
    //
    // 정말 다시 만들려면 Rebuild를 부른다 - 덮어쓰기 전에 프로젝트 루트 VfxBackup/<시각>/에
    // 사본을 남긴다.
    private const string BACKUP_FOLDER_NAME = "VfxBackup";
    private const string BACKUP_STAMP_FORMAT = "yyyyMMdd_HHmmss";
    private const string META_SUFFIX = ".meta";

    private sealed class MergedChild
    {
        public string SourceName;
        public string ChildName;

        // 붙인 뒤 바꿔 달 이름. 같은 원본에서 형제를 여럿 가져올 때 반드시 필요하다 -
        // Crack_BlueShock의 shock_up·shock_up (1)·shock_up (2)를 그대로 두면
        // ChildScales·RemovedChildren이 어느 것을 가리키는지 알 수 없다.
        public string RenameTo;

        // 붙인 사본 안에서 지울 자손. 원본의 다른 자식은 건드리지 않는다.
        public string[] RemovedDescendants = Array.Empty<string>();
    }

    private sealed class ChildScale
    {
        public string ChildName;
        public float Multiplier = 1f;
    }

    private sealed class ChildHeight
    {
        public string ChildName;
        public float LocalY;
    }

    private sealed class ChildGravity
    {
        public string ChildName;
        public float GravityModifier;
    }

    private sealed class BottomAlignment
    {
        // 이 자식의 메시 바닥 높이를 기준으로 삼는다.
        public string ReferenceChild;

        // 바닥을 기준에 맞춰 올리거나 내릴 자식들.
        public string[] Children = Array.Empty<string>();
    }

    private sealed class VfxDefinition
    {
        public string SourceName;
        public string OutputName;
        public string[] RemovedChildren = Array.Empty<string>();

        // 다른 원본에서 자식을 통째로 가져와 붙인다. 두 프리팹의 좋은 부분만 섞을 때 쓴다.
        public MergedChild[] MergedChildren = Array.Empty<MergedChild>();

        // 모든 자식의 X·Y 회전을 0으로 눕힌다. 기본은 끔.
        //
        // 처음에는 켜는 게 맞다고 봤다 - 2D 카메라(회전 0, z=-10)에서 X·Y 기울기는 입자를 화면
        // 안쪽으로 쏘니까. 그런데 실제로 놓고 보면 <b>원본 기울기가 그대로 맞다</b>. 이 게임은
        // 카메라를 기울이는 대신 타일 아트를 아이소메트릭으로 그리는 방식이라, 57도쯤 뒤로 누운
        // 메시가 화면에서 눌려 보이는 것이 곧 "셀 바닥에 놓인" 그림이 된다. 정면으로 세우면
        // 오히려 바닥을 뚫고 선 것처럼 보인다.
        public bool FlattensToScreenPlane;

        // 월드 XZ 평면에 고정된 자식(HorizontalBillboard)을 카메라를 보게 돌리고 세로만 절반으로
        // 눌러, 바닥에 누운 타원으로 만든다. 기본은 끔 - 위와 같은 이유로 원본 렌더 모드를
        // 함부로 바꾸지 않는다.
        //
        // 켜야 하는 경우는 분명하다. HorizontalBillboard는 회전 0인 이 프로젝트의 카메라에서
        // 옆날로 서서 아무것도 그리지 않으므로, 바닥 데칼을 가진 원본은 켜지 않으면 그 자식들이
        // 통째로 사라진다.
        //
        // 얼음·암석은 이 처리를 생성 후 Apply*DecalTuning으로 따로 걸었다. 화염은 여기서 한다 -
        // 그래야 Build 한 번으로 완성본이 나오고, 다시 돌려도 유지된다.
        public bool LaysDecalsFlat;

        // 자식 하나만 키우거나 줄인다. 두 원본을 섞을 때 반드시 필요하다 -
        // GroundCrack_Blue의 자식은 20~40, Crack_Blue02의 자식은 0.05~2.5로 authoring 기준이
        // 수십 배 다르다. 루트 배수 하나로는 한쪽이 반드시 안 맞는다.
        public ChildScale[] ChildScales = Array.Empty<ChildScale>();

        // 자식의 로컬 Y만 옮긴다. 여러 이미터가 같은 높이에서 솟아야 할 때 쓴다 -
        // 원본은 바위마다 높이가 조금씩 달라(-0.75 ~ -1.0) 어떤 것은 지면 아래에서 올라온다.
        public ChildHeight[] ChildHeights = Array.Empty<ChildHeight>();

        // 메시 바닥을 기준 자식과 같은 높이로 맞춘다.
        //
        // 좌표를 같게 두는 것만으로는 부족하다 - 메시 파티클이 화면에서 어디부터 시작하는지는
        // 피벗 위치와 눕힌 각도가 정한다. Object01은 피벗이 메시 한가운데라 기준점을 맞춰도
        // 절반(3.1~3.3)이 지면 아래로 내려가고, 피벗이 밑동에 있는 Mountain만 지면에서 시작한다.
        public BottomAlignment BottomAlignedChildren;

        // 자식의 중력 계수. 음수면 입자가 위로 뜬다(연기·재).
        //
        // 여기에 적어 두는 이유: 이 스크립트의 산출물은 다시 AssignTowerProjectiles가 복제해
        // Impact_Tower_*를 만들고 게임은 그쪽만 본다. 산출물을 손으로 고치면 이 스크립트를
        // 다시 돌리는 순간 사라지고, 복제본을 손으로 고치면 생성기를 돌리는 순간 사라진다.
        // 유지되어야 하는 값은 반드시 정의에 둔다.
        public ChildGravity[] ChildGravities = Array.Empty<ChildGravity>();

        public float ScaleMultiplier = 1f;
    }

    private static readonly VfxDefinition[] DEFINITIONS =
    {
        // 얼음 명중. 바탕은 FX_Crack_Blue의 URP판이다 - 이름만 FX_GroundCrack_Blue로 다를 뿐
        // 자식 구성(nova·glow·ground·blue_crack·stone·debris·air)이 완전히 같다.
        //
        // 여기에 FX_Crack_Bluerock의 rock1~rock6을 얹는다 - 이 여섯 개가 석영이 솟구치는 그림이다
        // (Mesh Object07 + biang, 수명 1.3초). 처음에는 Crack_Blue02에서 찾다가 헛짚었다.
        //
        // 원본은 여섯 개를 지면(XZ)에 부채꼴로 눕혀 두었는데(회전 57~64도의 X·Y),
        // X·Y를 0으로 눕히면 남은 Z가 그대로 화면 안에서의 부채꼴이 된다(68·104·140·152·156·247도).
        //
        // stone도 지우지 않고 회전만 눕힌다 - 원본이 XZ로 기울여 둬서 돌이 화면 안쪽으로
        // 날아가 버렸을 뿐, 돌이 솟는 그림 자체는 이 자식이 만든다.
        new VfxDefinition
        {
            SourceName = "FX_GroundCrack_Blue",
            OutputName = "FX_Impact_IceCrack",
            MergedChildren = new[]
            {
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock1" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock2" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock3" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock4" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock5" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock6" },

                // 석영만 솟고 주변이 조용해서 빛 파열을 얹는다. FX_Crack_BlueShock에서 가져오는데,
                // 이 원본에서 "터져 나가는" 그림을 만드는 건 shock_up 세 장뿐이다
                // (Mesh PlaneL, 수명 0.22). 나머지 자식은 바탕이 이미 갖고 있다
                // (glow·glowdi·ground·stone이 이름까지 겹친다 - 그래서 아래처럼 이름을 바꿔 단다).
                //
                // ring02도 한때 얹었다가 뺐다. 유효 0.96칸에 수명 0.35초라 가장 잘 보여야 하는데,
                // 이것만 켜고 끈 그림을 나란히 찍어 보면 두 장이 구분되지 않는다 -
                // 바탕의 nova·glow(1.5·2칸)가 같은 자리에 더 밝게 깔려 그냥 묻힌다.
                //
                // 세 장의 각도(292,277,25)·(298,38,171)·(292,269,310)가 부채꼴을 만든다.
                // FlattensToScreenPlane를 켜면 이게 무너지므로 이 정의에서는 끈 채로 둔다.
                new MergedChild
                {
                    SourceName = "FX_Crack_BlueShock", ChildName = "shock_up", RenameTo = "BurstPlane1"
                },
                new MergedChild
                {
                    SourceName = "FX_Crack_BlueShock", ChildName = "shock_up (1)", RenameTo = "BurstPlane2"
                },
                new MergedChild
                {
                    SourceName = "FX_Crack_BlueShock", ChildName = "shock_up (2)", RenameTo = "BurstPlane3"
                },
                // 위 셋만으로는 파열이 석영 위쪽 중앙에만 몰린다(원본 각도가 그렇게 잡혀 있다).
                // 옆으로 퍼지는 몫은 FX_Crack_Blue02에서 가져온다 -
                // lms는 6발 버스트라 한 번에 여섯 갈래로 뻗고, Flare는 수명 0.15초짜리 섬광이다.
                new MergedChild
                {
                    SourceName = "FX_Crack_Blue02", ChildName = "lms", RenameTo = "BurstStreaks"
                },
                new MergedChild
                {
                    SourceName = "FX_Crack_Blue02", ChildName = "Flare", RenameTo = "BurstFlare"
                }
            },
            // 석영에는 배수를 주지 않는다. 한때 다섯 배로 키웠는데, startSize만 보고 "0.12라 작다"고
            // 판단한 것이 틀렸다 - 메시 파티클의 화면 크기는 <b>메시 바운즈까지</b> 곱해진다.
            // Object07의 긴 축이 2.19라 다섯 배면 석영 하나가 1.5~2.3칸짜리가 됐다.
            // 배수 없이 두면 0.32~0.46칸으로, 균열 위에 얹히는 파편다운 크기가 된다.
            //
            // 얹어 온 다섯에는 배수가 필요하다. MergeChildren이 자식을 <b>루트에 바로</b> 붙이므로
            // 원본에서 부모였던 컨테이너의 스케일이 사라지기 때문이다(BlueShock의 smoke가 1.3,
            // Blue02의 "ground (3)"이 9). 그것부터 되돌리고, 그 위에 화면에서 읽힐 만큼 더 준다.
            // 계측 기준은 바탕 균열 원반 ground=1.15칸, 석영 rock1=0.23칸이다.
            //   BurstPlane* : 0.275칸 x2.6 = 0.72칸 - 파열 끝이 균열 원반 가장자리에 닿는다
            //                 (PlaneL의 긴 축이 2라 메시 바운즈까지 이미 곱해진 값이다)
            ChildScales = new[]
            {
                new ChildScale { ChildName = "stone", Multiplier = 3f },
                new ChildScale { ChildName = "BurstPlane1", Multiplier = 2.6f },
                new ChildScale { ChildName = "BurstPlane2", Multiplier = 2.6f },
                new ChildScale { ChildName = "BurstPlane3", Multiplier = 2.6f },
                // Blue02에서 온 둘은 사라지는 부모 스케일이 9다(컨테이너 "ground (3)").
                // 되돌리기만 하면 BurstStreaks가 0.25칸이라 석영에 묻힌다 - 0.7칸까지 키운다.
                //   BurstStreaks : 0.028칸 x25 = 0.7칸
                //   BurstFlare   : 0.125칸 x7  = 0.88칸 (균열 원반 1.15칸보다 작게 둔다)
                new ChildScale { ChildName = "BurstStreaks", Multiplier = 25f },
                new ChildScale { ChildName = "BurstFlare", Multiplier = 7f }
            },
            ScaleMultiplier = 0.05f
        },

        // 암석 명중. 얼음과 달리 바탕 하나로 간다 - FX_Crack_Rock 하나가 바위 덩이
        // (rock·rock1~3·Mountain)와 지면 균열(ring·glow·glowdi·ground)을 이미 다 갖고 있다.
        //
        // 한때 FX_Crack_RockAOE를 통째로 얹어 광역 표시를 더했다. 암석 타워가 유일한 광역
        // (반경 2)이라 어디까지 맞는지 보여줄 바닥 원이 필요했기 때문이다. 뺐다 -
        // 이 팩의 AOE는 3D 지면을 내려다보는 카메라를 전제로 한 큰 바닥 판이라, 정사영 2D에
        // 맞추려고 빌보드로 세우면 반경을 알려주기는커녕 솟아오른 바위를 정면에서 덮어 버린다.
        // 어떤 크기·색 배수를 줘도 "가리거나, 안 보이거나" 둘 중 하나였다.
        //
        // 대신 들어간 것은 없다. 남은 지면 균열(ring)은 2.88칸이라 반경 2(지름 4칸)에 못 미치니
        // 반경 표시로 읽으면 안 된다 - 광역 사거리를 보여줘야 한다면 별도로 만들어야 한다.
        new VfxDefinition
        {
            SourceName = "FX_Crack_Rock",
            OutputName = "FX_Impact_StoneCrack",
            MergedChildren = new[]
            {
                // 날아온 탄이 쪼개지는 순간. Hovl 팩의 "Hit 24 green explosion"을 쓰는데,
                // 이 프로젝트에는 이미 가공본이 Impact_V1_24_green_explosion으로 들어와 있어
                // 원본(Assets/Hovl Studio/...)이 아니라 그쪽을 가져온다.
                //
                // 초록이지만 색 걱정은 없다 - 세트 색조(HueDegrees 280)가 임팩트 전체에 걸리므로
                // 복제 시점에 보라로 돌아간다. 흰색인 Debris는 채도가 0이라 그대로 남는다.
                new MergedChild
                {
                    SourceName = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_24_green_explosion.prefab",
                    ChildName = "Impact_V1_24_green_explosion",
                    RenameTo = "Burst"
                }
            },
            // rock2만 원본이 유독 크다(0.99칸). 형제 바위들이 0.51~0.56이라 혼자 두 배로 튄다.
            //
            // Burst는 원본 최대 4에 루트 0.08이 곱해져 0.32칸까지 줄어든다. 탄이 쪼개지는 순간을
            // 알릴 만큼은 보여야 하므로 2.5배로 되돌려 0.8칸에 둔다 - 바위(0.5)보다는 크고
            // 광역 표시(3.4)보다는 한참 작은, 가운데서 터지는 크기다.
            ChildScales = new[]
            {
                new ChildScale { ChildName = "rock2", Multiplier = 0.55f },
                new ChildScale { ChildName = "Burst", Multiplier = 2.5f }
            },
            // 바위 네 개가 Mountain과 같은 지면에서 솟게 한다. 좌표를 -0.98로 같게 맞춰 봤지만
            // 소용이 없었다 - Object01은 피벗이 메시 한가운데라 좌표를 맞춰도 절반(3.1~3.3)이
            // 지면 아래로 내려간다. 그래서 좌표가 아니라 메시 바닥을 기준에 맞춘다.
            // X·Z는 건드리지 않아 흩어진 배치는 그대로 남는다.
            BottomAlignedChildren = new BottomAlignment
            {
                ReferenceChild = "Mountain",
                Children = new[] { "rock", "rock1", "rock2", "rock3" }
            },
            // 파열 연기를 위로 띄운다. 대문자 Smoke는 Burst(green_explosion)의 것이다 -
            // 바탕 FX_Crack_Rock에는 연기 이미터가 없어 이름이 겹칠 일이 없다.
            ChildGravities = new[]
            {
                new ChildGravity { ChildName = "Smoke", GravityModifier = -0.22f }
            },
            ScaleMultiplier = 0.08f
        },

        // 화염 명중. "바닥에 불이 깔리고 그 위로 불덩이가 솟는다"를 두 원본으로 나눠 만든다.
        //
        // 바탕은 이 작업 전까지 화염 타워가 쓰던 Impact_Fire_V1이다. 자식 이름이
        // Derbis·Sparks·BigSparks·GlowBlack이라 한 번 "불기둥이 없다"고 넘겼는데, 실제로 구워 보니
        // Derbis가 소용돌이치는 불덩이였다(위 0.26 -> 0.68로 떠오른다). <b>이 팩들은 이름과 그림이
        // 자주 어긋나므로 이름으로 거르지 말 것.</b>
        //
        // Sparks·BigSparks는 사방으로 튀는 노란 점이다. 폭 1.95까지 흩어지면서 픽셀은 379뿐이라
        // 화면만 어지럽히고 실루엣에는 기여하지 않는다.
        //
        // 한때 FX_RealisticEXP_B01을 바탕으로 썼다가 물렀다 - 불덩이·먼지·불티·별반짝임이 한꺼번에
        // 터져 너무 화려했다. FX_Crack_Risingfire도 물렀다 - 자식 열 개가 전부 연속 방출이라
        // 명중 순간이 비고(t=0.06에 82픽셀) 반대로 t=2.0에도 안 꺼졌다.
        //
        // 바닥은 Risingfire에서 Flash 하나만 꺼내 온다. 위가 0.06~0.14로 선에 붙어 있고
        // 0.3초에 4402픽셀로 절정을 찍은 뒤 0.6초에 659까지 빠진다. 같은 원본의 flame은
        // 반대로 0.6초에 7221까지 자라며 안 꺼져서 쓰지 않는다.
        new VfxDefinition
        {
            SourceName = "Assets/Imported/Prefabs/Projectile/Impact_Fire_V1.prefab",
            OutputName = "FX_Impact_FireCrack",
            RemovedChildren = new[] { "Sparks", "BigSparks" },
            MergedChildren = new[]
            {
                new MergedChild
                {
                    SourceName = "FX_Crack_Risingfire",
                    ChildName = "Flash",
                    RenameTo = "Floorfire"
                }
            },
            // Flash는 Risingfire에서 루트 0.11로 authoring된 것인데 이쪽 루트는 0.84다.
            // 그대로 얹으면 유효 크기가 36까지 뛰어 화면을 통째로 덮는다.
            // 0.13은 Risingfire에서 좋아 보였던 유효 크기 3.3에 맞춘 값이다.
            ChildScales = new[]
            {
                new ChildScale { ChildName = "Floorfire", Multiplier = 0.13f }
            },
            LaysDecalsFlat = true,

            // 2배로 뒀더니 불덩이가 위 1.01까지 떠올라 바닥 불이 묻혔다(얼음 0.44, 암석 0.47).
            // 불덩이를 줄여야 바닥이 보인다 - 바닥을 키우면 불덩이까지 같이 커진다.
            ScaleMultiplier = 1.4f
        },

        // 불 화상. Glow·Glow_Ground는 머티리얼이 Unity 기본값(Default-ParticleSystem)이라
        // 작가가 칠한 그림이 아니라 흰 덩어리다 - 빼면 Eric 셰이더로 그린 불꽃만 남는다.
        // Pos는 빼면 안 된다 - 불꽃 세 개를 담고 있는 컨테이너라 같이 사라진다(실제로 그렇게 날렸다).
        new VfxDefinition
        {
            SourceName = "FX_Dust_Fire",
            OutputName = "FX_Status_FireBurn",
            RemovedChildren = new[] { "Glow", "Glow_Ground" },
            ScaleMultiplier = 0.18f
        }
    };

    // 없는 산출물만 만든다. 이미 있는 것은 손튜닝이 얹혀 있을 수 있으므로 건드리지 않는다.
    public static string Build()
    {
        return Run(false);
    }

    // 있는 것까지 전부 다시 만든다. 덮어쓰기 전에 사본을 남기지만,
    // Apply*DecalTuning 계열을 다시 돌려야 손튜닝이 복구된다.
    public static string Rebuild()
    {
        return Run(true);
    }

    private static string Run(bool overwrites)
    {
        EnsureOutputFolder();
        var report = new StringBuilder();
        string backupFolder = null;

        foreach (VfxDefinition definition in DEFINITIONS)
        {
            string outputPath = $"{OUTPUT_FOLDER}/{definition.OutputName}.prefab";

            if (File.Exists(outputPath))
            {
                if (!overwrites)
                {
                    report.AppendLine($"건너뜀(이미 있음): {outputPath}");
                    continue;
                }

                backupFolder = BackUp(outputPath, backupFolder, report);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ResolveSourcePath(definition.SourceName));

            if (root == null)
            {
                throw new InvalidOperationException($"원본을 열 수 없습니다: {definition.SourceName}");
            }

            try
            {
                root.name = definition.OutputName;
                RemoveNamedChildren(root, definition.RemovedChildren);
                MergeChildren(root, definition.MergedChildren);
                FlattenRotations(root, definition.FlattensToScreenPlane);
                ApplyChildScales(root, definition.ChildScales);
                ApplyChildHeights(root, definition.ChildHeights);
                AlignBottoms(root, definition.BottomAlignedChildren);
                ApplyChildGravities(root, definition.ChildGravities);
                root.transform.localScale *= definition.ScaleMultiplier;
                LayDecalsFlat(root, definition.LaysDecalsFlat);
                ForceSortingLayer(root);
                PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            report.AppendLine(Describe(outputPath));
        }

        backupFolder = DeleteStale($"{OUTPUT_FOLDER}/FX_Status_IceSlow.prefab", backupFolder, report);

        if (backupFolder != null)
        {
            report.AppendLine($"사본: {backupFolder}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report.ToString();
    }

    // 다른 원본을 열어 지정한 자식만 복제해 붙인다. 원본은 열었다가 그대로 닫으므로 변하지 않는다.
    private static void MergeChildren(GameObject root, IReadOnlyList<MergedChild> merged)
    {
        foreach (MergedChild entry in merged)
        {
            GameObject donor = PrefabUtility.LoadPrefabContents(ResolveSourcePath(entry.SourceName));

            if (donor == null)
            {
                throw new InvalidOperationException($"섞을 원본을 열 수 없습니다: {entry.SourceName}");
            }

            try
            {
                Transform source = donor.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == entry.ChildName);

                if (source == null)
                {
                    throw new InvalidOperationException(
                        $"{entry.SourceName}에 {entry.ChildName} 자식이 없습니다.");
                }

                var copy = UnityEngine.Object.Instantiate(source.gameObject, root.transform);
                copy.name = string.IsNullOrEmpty(entry.RenameTo) ? entry.ChildName : entry.RenameTo;
                RemoveNamedChildren(copy, entry.RemovedDescendants);
                copy.transform.localPosition = source.localPosition;
                copy.transform.localRotation = source.localRotation;
                copy.transform.localScale = source.localScale;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(donor);
            }
        }
    }

    private static void ApplyChildGravities(GameObject root, IReadOnlyList<ChildGravity> gravities)
    {
        foreach (ChildGravity entry in gravities)
        {
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.gameObject.name != entry.ChildName)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                main.gravityModifier = new ParticleSystem.MinMaxCurve(entry.GravityModifier);
            }
        }
    }

    private static void AlignBottoms(GameObject root, BottomAlignment alignment)
    {
        if (alignment == null)
        {
            return;
        }

        Transform reference = FindChild(root, alignment.ReferenceChild);

        if (reference == null)
        {
            throw new InvalidOperationException($"기준 자식을 찾을 수 없습니다: {alignment.ReferenceChild}");
        }

        float targetBottom = reference.localPosition.y + MeshBottomOffset(reference);

        foreach (string name in alignment.Children)
        {
            Transform child = FindChild(root, name);

            if (child == null)
            {
                throw new InvalidOperationException($"맞출 자식을 찾을 수 없습니다: {name}");
            }

            Vector3 position = child.localPosition;
            child.localPosition = new Vector3(position.x, targetBottom - MeshBottomOffset(child), position.z);
        }
    }

    // 기준점에서 메시 바닥까지의 거리(보통 음수). 회전·스케일·startSize를 모두 먹인 값이다.
    private static float MeshBottomOffset(Transform child)
    {
        var ps = child.GetComponent<ParticleSystem>();
        var renderer = child.GetComponent<ParticleSystemRenderer>();

        if (ps == null || renderer == null || renderer.mesh == null)
        {
            return 0f;
        }

        Bounds bounds = renderer.mesh.bounds;
        Vector3 scale = child.localScale * ps.main.startSize.constant;
        Quaternion rotation = child.localRotation;
        float minY = float.MaxValue;

        for (int corner = 0; corner < 8; corner++)
        {
            var local = new Vector3(
                (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                (corner & 4) == 0 ? bounds.min.z : bounds.max.z);

            minY = Mathf.Min(minY, (rotation * Vector3.Scale(local, scale)).y);
        }

        return minY;
    }

    // 이름만 주면 Eric 팩(URP)에서 찾고, 슬래시가 있으면 프로젝트 전체 경로로 본다 -
    // 다른 팩의 이펙트를 섞을 때 필요하다(암석 파열에 쓰는 AAA_Vol1의 폭발이 그렇다).
    private static string ResolveSourcePath(string source)
    {
        return source.Contains("/") ? source : $"{SOURCE_FOLDER}/{source}.prefab";
    }

    private static Transform FindChild(GameObject root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }

    private static void ApplyChildHeights(GameObject root, IReadOnlyList<ChildHeight> heights)
    {
        foreach (ChildHeight entry in heights)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != entry.ChildName)
                {
                    continue;
                }

                Vector3 position = child.localPosition;
                child.localPosition = new Vector3(position.x, entry.LocalY, position.z);
            }
        }
    }

    private static void ApplyChildScales(GameObject root, IReadOnlyList<ChildScale> scales)
    {
        foreach (ChildScale entry in scales)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == entry.ChildName)
                {
                    child.localScale *= entry.Multiplier;
                }
            }
        }
    }

    private static void FlattenRotations(GameObject root, bool flattens)
    {
        if (!flattens)
        {
            return;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root.transform)
            {
                continue;
            }

            Vector3 angles = child.localEulerAngles;
            child.localEulerAngles = new Vector3(0f, 0f, angles.z);
        }
    }

    // HorizontalBillboard는 XZ 평면에 눕는다. 카메라가 +Z를 내려다보는 2D라 그 판은 옆면만 보여
    // 사실상 사라진다. Billboard는 언제나 카메라를 향하므로 그대로 쓸 수 있다.
    // 아이소메트릭 셀이 1 x 0.5(Grid.prefab m_CellSize)이므로, 바닥에 누운 원은 화면에서 2:1 타원이다.
    private const float ISO_SQUASH = 0.5f;

    private static void LayDecalsFlat(GameObject root, bool lays)
    {
        if (!lays)
        {
            return;
        }

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer == null)
            {
                continue;
            }

            // 세로로 선 것은 눕히기만 하고 누르지 않는다. 바닥에 깔린 그림이 아니라
            // 원래 서 있어야 하는 그림이라 세로를 절반으로 만들면 찌그러진다.
            if (renderer.renderMode == ParticleSystemRenderMode.VerticalBillboard)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                continue;
            }

            if (renderer.renderMode != ParticleSystemRenderMode.HorizontalBillboard)
            {
                continue;
            }

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;

            var main = ps.main;
            ParticleSystem.MinMaxCurve width = main.startSizeX;

            main.startSize3D = true;
            main.startSizeY = ScaledCurve(width, ISO_SQUASH);
            main.startSizeZ = width;
        }
    }

    private static ParticleSystem.MinMaxCurve ScaledCurve(ParticleSystem.MinMaxCurve source, float multiplier)
    {
        switch (source.mode)
        {
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(
                    source.constantMin * multiplier, source.constantMax * multiplier);

            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier, source.curve);

            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier, source.curveMin, source.curveMax);

            default:
                return new ParticleSystem.MinMaxCurve(source.constant * multiplier);
        }
    }

    private static void ForceSortingLayer(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingLayerName = SORTING_LAYER;
        }
    }

    private static void RemoveNamedChildren(GameObject root, IReadOnlyCollection<string> removedNames)
    {
        if (removedNames.Count == 0)
        {
            return;
        }

        HashSet<string> names = removedNames.ToHashSet();
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int index = transforms.Length - 1; index >= 0; index--)
        {
            Transform child = transforms[index];

            if (child != root.transform && names.Contains(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Imported/Prefabs/Effects"))
        {
            AssetDatabase.CreateFolder("Assets/Imported/Prefabs", "Effects");
        }

        if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Imported/Prefabs/Effects", "Status");
        }
    }

    private static string DeleteStale(string path, string backupFolder, StringBuilder report)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            backupFolder = BackUp(path, backupFolder, report);
            AssetDatabase.DeleteAsset(path);
            report.AppendLine($"삭제: {path} (이름이 바뀌어 남은 옛 산출물)");
        }

        return backupFolder;
    }

    // 덮어쓰거나 지우기 직전의 파일을 프로젝트 밖(Assets 아래가 아닌 곳)에 복사해 둔다.
    // Assets 안에 두면 유니티가 GUID가 같은 프리팹을 하나 더 임포트해 참조가 꼬인다.
    //
    // 폴더는 실제로 백업할 것이 생겼을 때만 만든다 - 아무것도 덮어쓰지 않은 실행이
    // 빈 폴더를 남기지 않게 한다.
    private static string BackUp(string assetPath, string backupFolder, StringBuilder report)
    {
        if (backupFolder == null)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string stamp = DateTime.Now.ToString(BACKUP_STAMP_FORMAT);
            backupFolder = Path.Combine(projectRoot, BACKUP_FOLDER_NAME, stamp);
            Directory.CreateDirectory(backupFolder);
        }

        string fileName = Path.GetFileName(assetPath);
        File.Copy(assetPath, Path.Combine(backupFolder, fileName), true);

        // .meta도 같이 남긴다. GUID가 여기 들어 있어, 이것 없이 되돌리면 씬·데이터 참조가 끊긴다.
        string metaPath = assetPath + META_SUFFIX;

        if (File.Exists(metaPath))
        {
            File.Copy(metaPath, Path.Combine(backupFolder, fileName + META_SUFFIX), true);
        }

        report.AppendLine($"  백업: {fileName}");

        return backupFolder;
    }

    private static string Describe(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var sb = new StringBuilder();

        bool anyChildLoops = false;

        sb.AppendLine($"== {root.name}  rootScale={root.transform.localScale.x:0.###}");

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            // 컨테이너 역할만 하는 시스템은 판정에서 뺀다 - 머티리얼이 Unity 기본값이면
            // 작가가 그린 그림이 아니라 자식을 굴리기 위한 껍데기다.
            bool isDriver = renderer == null || renderer.sharedMaterial == null ||
                renderer.sharedMaterial.name.StartsWith("Default-Particle");

            if (ps.transform != root.transform && !isDriver)
            {
                anyChildLoops |= main.loop;
            }

            sb.Append("   ").Append(ps.gameObject.name.PadRight(18));
            sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(10));
            // 자식 자신의 스케일까지 포함해야 화면에서 실제로 얼마만 한지 나온다.
            // 메시 파티클은 메시 바운즈까지 곱해야 실제 화면 크기가 나온다 - startSize만 보면
            // 몇 배씩 빗나간다(석영을 다섯 배로 키웠다가 화면을 덮은 것이 그 때문이다).
            Vector3 bounds = renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh &&
                renderer.mesh != null
                ? renderer.mesh.bounds.size
                : Vector3.one;

            Vector3 lossy = ps.transform.lossyScale;
            float longest = Mathf.Max(
                bounds.x * lossy.x, Mathf.Max(bounds.y * lossy.y, bounds.z * lossy.z)) * main.startSize.constant;

            sb.Append(" eff=").Append(longest.ToString("0.###").PadRight(7));
            sb.Append(" loop=").Append(main.loop ? "1" : "0");
            sb.Append(" rot=").Append(ps.transform.localEulerAngles.ToString("0").PadRight(15));
            sb.Append(" layer=").Append(renderer == null ? "-" : renderer.sortingLayerName);
            sb.AppendLine();
        }

        sb.AppendLine(anyChildLoops ? "   -> 지속형(상태 표시용)" : "   -> 일회성(명중 연출용)");
        return sb.ToString();
    }
}
