# 개발 진행 순서

## 현재 확정된 우선순위

1. 플레이어 조작 코어
2. 기본 공격과 피격
3. 추격형 적 1종
4. 웨이브 루프
5. 업그레이드 선택
6. 돌진형, 사격형, 방패형 적
7. 보스전
8. UI, 사운드, 이펙트 강화
9. 저장과 결과 화면
10. 밸런싱과 빌드

## 1차 구현 목표

Unity 씬에서 플레이어 오브젝트 하나를 만들고 다음 기능을 테스트할 수 있게 한다.

- WASD 이동
- 마우스 조준
- Space 대시
- 좌클릭 기본 공격
- 체력과 피격 처리
- 넉백 처리

## 1차 씬 구성 기준

플레이어 오브젝트에 다음 컴포넌트를 붙인다.

- Rigidbody2D
- Collider2D
- PlayerInputReader
- PlayerMovement2D
- PlayerAim2D
- PlayerDash2D
- MeleeAttack2D
- Health

적 테스트 오브젝트에는 다음 컴포넌트를 붙인다.

- Rigidbody2D
- Collider2D
- Health
- KnockbackReceiver2D

