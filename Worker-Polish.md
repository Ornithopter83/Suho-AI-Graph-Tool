# Worker-Polish — ProjectHub Worker 정책

Master-Polish.md를 읽기 전용 최상위 기준으로 삼고, 실행 경계는 이 문서와 역할 계약으로 정한다.

제1조 (역할)
① 화면 순서: HQ → SuperVisor → WORK → RESOURCE → HIGH.
② HQ는 목표·마일스톤·최종 판단을 담당한다. SuperVisor는 작업 분석·배정·결과 통합을 담당한다.
③ 실제 파일 조사·구현·테스트·검증은 단발성 WORK에 우선 위임한다. WORK는 재위임·Git 최종화를 하지 않는다.
④ HIGH는 읽기 전용 검증 WORK의 결과를 판단하고 수정 요구를 보고한다. 직접 구현하지 않는다.
⑤ RESOURCE는 별도 이미지 생성 sidecar이며 기존 비차단 규칙을 유지한다.

제2조 (세션·위임: UnityWorker 실행 방식)
① SuperVisor·HIGH는 각자 새 Provider 세션(SessionId=null)에서 delegate_work(task, paths, read_only)를 호출한다.
② MCP delegate_work는 UnityWorker와 동일하게 호출 즉시 WORK를 독립 신규 세션으로 실행한다. 부모 CLI는 도구 응답을 기다린다. 도구 응답은 접수가 아닌 실제 WORK 실행 결과다.
③ 부모와 WORK 세션의 실행 시간이 겹치는 것은 정상이다. 같은 부모 토큰의 MCP 호출은 SerialCalls로 직렬화하고, 모든 임시 WORK 실행은 하나의 _temporaryWorkSerial로 직렬화한다.
④ 부모는 반환된 WORK 결과·오류를 직접 확인하고 필요한 다음 과업을 위임한 뒤 같은 부모 세션에서 통합 보고한다. 큐 등록·부모 종료 후 WORK 실행·별도 통합 세션은 사용하지 않는다.
⑤ WORK는 매 호출마다 새 세션·독립 MCP 토큰을 사용하고, 부모 위임 도구를 받지 않는다. HQ용 WORK 번호·체크포인트를 만들지 않는다.
⑥ 최소 1회의 성공한 WORK 위임이 없으면 completed를 인정하지 않는다. 범위 위반·경로 거부·실행 실패는 별개로 기록하고 실제 오류를 보고한다.
⑦ WORK가 실패해도 부모가 직접 구현·테스트를 대신하지 않는다. 부모는 재위임 여부와 차단을 판단한다.

제3조 (경계·최종화)
① WORK 범위는 프로젝트 상대경로 1~5개이며 기존 파일·dirty 상태·심볼릭 링크·동시 변경을 검증한다.
② HIGH 하위 WORK와 READ_ONLY_NO_FILE_CHANGES 정책의 모든 WORK는 읽기 전용이다.
③ 기계적 BUILD 및 Git 최종화는 Worker가 담당하며 실제로 건너뛴 검증은 SKIPPED로 기록한다.
④ RESOURCE는 독립적인 비차단 sidecar이며 미검증 결과를 완료로 보고하지 않는다.
⑤ 모든 위임의 호출·성공·실패·토큰·변경 범위는 기계적으로 추적한다.

제4조 (출력 검증)
① [KEY], [GOTO], [ACTION], [RESPONSE] 대괄호 제어문의 종류·순서·완료 표식은 엄격 검증한다.
② @@ 구분과 <FIELD>는 AI용 권고 형식이며 Worker의 완료·실패 판정 근거가 아니다. 대괄호 제어문 외 누락·불일치·상태값 오류로 결과를 차단하거나 재요청하지 않고 원문을 그대로 다음 역할에 전달한다.
③ 실제 위임 역할, 허용 경로, 쓰기 정책, Git 최종화 및 필수 실행 대상의 안전성은 별도 코드 검증을 유지한다.
④ WORK→SuperVisor/HIGH→HQ의 원문 및 실행 증거는 길이를 잘라 대체하지 않는다. 실제 CLI 실패·안전 위반은 원문과 별도로 기록한다.
⑤ 작업 결과 의미의 판정·재위임·재조사는 SuperVisor/HIGH의 책임이다. AI 보고 상태를 추출할 수 없어 자동 완료를 확정하지 못하면 Git 최종화는 보류하고 HQ에 원문을 보고한다.
