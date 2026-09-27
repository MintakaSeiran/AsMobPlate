# AS Mob Plate

Small Dalamud dev plugin for showing and tracking only FFXIV A-rank and S-rank hunt marks.

## Version

Current release: `1.0.008`.

Author: MintakaSeiran.

## Plugin Images

The plugin installer uses `images/icon.png` for its square thumbnail and `images/image1.png` for the original gameplay screenshot. Both images are copied beside the DLL under `images/` for local dev-plugin loading. The manifest also points to the repository-hosted images.

修正を反映するたびに末尾を1ずつ増やします: `1.0.005` → `1.0.006` → `1.0.007`。
正本は `AsMobPlate.csproj` の `Version` です。.NET/Dalamud側の数値バージョンは先頭ゼロが省略され、`1.0.005` は `1.0.5.0` と表示される場合があります。
`Configuration.Version` は設定移行用の番号で、プラグインのリリース番号とは別管理です。

## Detection

- Scans `IObjectTable` every frame.
- Only `IBattleNpc` objects are considered.
- A/S rank detection is based only on `IBattleNpc.NameId`.
- The NameId allow-list lives in `Hunts/HuntMarkRegistry.cs`.
- Normal enemies, B ranks, FATE mobs, NPCs, and players are ignored because their NameId is not in the A/S registry.
- Dead or invalid mobs are skipped when `MaxHp == 0` or `CurrentHp == 0`.

## Distances

- `Max display distance`
  - Controls overhead nameplate rendering.
  - Default: `2000 yalms`.
  - Max setting: `2000 yalms`.
- `A rank notification distance`
  - Controls sound/TTS/chat/map-flag detection for A ranks.
  - Default: `200 yalms`.
- `S rank notification distance`
  - Controls sound/TTS/chat/map-flag detection for S ranks.
  - Default: `1000 yalms`.
  - Max setting: `2000 yalms`.

S ranks can be detected even when they are too far away for an overhead plate, as long as they are present in the object table.

## Nameplate

The plate is drawn from `UiBuilder.Draw` using `ImGui.GetForegroundDrawList()`.

Displayed content can include:

- `[A] name` or `[S] name`
- optional `ObjectIndex`
- HP bar
- HP percent
- distance in yalms
- estimated time to kill
- announced start ET and local countdown
- `討伐中` label when the announced start time has passed
- optional countdown frame that shrinks clockwise from the top-right corner during the countdown window

The plate anchor is computed from `npc.Position` and `npc.HitboxRadius`, with an adjustable Y offset.

`Countdown frame thickness` adjusts only the countdown outline, from 1 to 10 px (default: 2 px at Scale 1.0). The overall `Scale` still scales the stroke; rendering uses a minimum of 1 px. Increasing this setting moves the stroke outward so it does not cover the name or HP bar. Changes are saved automatically; existing configurations use the default thickness.

## HP ETA

- `Overlay/HpTracker.cs` stores per-mob HP samples.
- ETA is estimated from HP lost over time:
  - `hpPerSecond = lostHp / elapsedSeconds`
  - `eta = CurrentHp / hpPerSecond`
- If HP has not dropped, the sample is too new, or HP increased, ETA is shown as `--:--`.

## Notifications

When a hunt enters its notification range:

- FFXIV chat sound effect can play.
- Default is 4 repeats.
- TTS can speak a configurable message.
- Chat notification can be printed.
- The map can be opened with a flag at the mob position.

Each object is notified/flagged once while it remains in range. If it disappears or leaves range, it can notify again later. A cooldown also prevents rapid repeat notifications for the same object.

## Announced Start Time

`Overlay/AnnouncedStartTimeTracker.cs` watches chat for start-time announcements.

Recognized examples:

- `ET 22:30`
- `ET830`
- `ET 830`
- `830`
- `22:30開始`
- `22:30 start`
- `pull 22:30`

Messages must contain one of these keywords:

- `開始`
- `スタート`
- `start`
- `pull`
- `ET`

### ET 計算仕様 (v9)

- 時刻源は `Framework.Instance()->ClientTime.EorzeaTime`。ゲームが管理するETを直接読み取ります。
- 告知を処理した瞬間のETを秒単位で保持します。`Show ET at announcement` はその時刻を「時:分」で表示し、計算では秒を切り捨てません。
- チャットのタイムスタンプとPC時計はログ用です。開始判定・残り秒・自動起動の計算には使いません。
- 換算は `実時間の残り秒 = (開始ET秒 - 現在ET秒) * 7 / 144`。ET1分は `35/12 = 2.916666...` 秒、ET1時間は175秒です。
- 開始時刻 `ET1319` は `13:19:00` を意味します。受信時 `13:13:00` なら残り17.5秒、`13:13:30` なら残り約16.042秒です。画面の整数秒は切り上げます。
- 日付またぎを含めて最も近い開始時刻を選びます。`23:59 -> 00:01` は未来2分、`00:01 -> 23:59` は過去2分です。
- `ET announcement window` の既定は前後120 ET分。この範囲外の告知はログに記録して無視します。翌日までの長時間待ちは作りません。
- 既に開始ETを過ぎていれば残り0秒とし、既存の `討伐中` 表示と背景色へ切り替えます。開始後の表示時間は `Start time display after pull` で指定します。
- 同じ開始ETの再告知は無視し、最初の受信ETと自動起動済み状態を保持します。別の有効な開始ETが告知された場合は予約を更新します。
- ゲーム内部ETを毎 `IFramework.Update` で読み直します。描画・カメラの向き・モブの画面内外には依存しません。
- ログアウト、別テリトリーへの移動、内部ET取得不可、ゲームの時刻上書き状態では予約を破棄します。PC時計への自動代替はしません。
- 通常のチャット受信はゲームスレッドで即時取得します。他スレッドからの通知は次のゲーム更新へ移し、ログに `deferred=True` を残します。
- `EorzeaTime` のET秒まで使います。1 ET秒は実時間約0.049秒です。画面更新間隔・ゲーム側のカウントダウン受付遅延は別に発生します。

表示例:

```text
ET 13:13 -> 13:19  in 18s
```

### ゲーム標準カウントダウン

- `Countdown seconds` の既定は10秒、範囲は5～30秒です。この値を起動の何秒前かにも共用し、終了ETと告知ETを揃えます。外枠の縮小表示も同じ値を使います。
- 受信時に指定秒数以上残っていれば予約し、残り秒が指定値を下回る最初のゲーム更新で `/countdown N` を一度送ります。
- 指定秒数前に間に合わなかった告知は自動起動しません。ETと残り秒は表示します。
- フレーム停止などで起動点を0.5秒以上過ぎた場合も起動を見送ります。ゲーム側が拒否した場合を含め、毎フレームの再送はしません。
- `UIModule.ProcessChatBoxEntry` に固定形式の標準コマンドを渡します。ユーザーのチャット文章をコマンドとして実行しません。
- `ICommandManager.ProcessCommand` は登録されたプラグインコマンド用なので使用しません。
- PTなどのゲーム側条件で実行できない場合があります。ログの `countdown submitted` は送信済みという意味で、ゲームが受理したことの保証ではありません。

### 旧設定からの移行

設定バージョンは9です。旧 `ET clock correction` (既定-6分)、`ET minute real seconds` (旧3秒)、`Countdown start before` は廃止しました。再読み込み時に旧設定値は計算から外れ、設定保存時に取り除かれます。その他の既存設定は保持します。

`ET announcement window` は互換性のため旧 `EtStartPastToleranceMinutes` の保存キーを引き継ぎますが、v9では前後両方向の許容範囲として使用します。

## Debug Log

The settings window includes a debug log section for testing announced start timing.

- `Copy debug log` copies the current in-memory log.
- `Dump debug log` writes it to `%APPDATA%\XIVLauncher\devPlugins\AsMobPlate\debug-log.txt`.
- `Clear debug log` clears the in-memory log.

`Game ET: HH:mm:ss` shows the live internal clock in settings, even without a hunt in view.

The log records the timing version, clock source, chat timestamp (diagnostic only), local receipt time, raw game ET, received ET including seconds, target ET, ET-second delta, real seconds remaining, estimated local start time (diagnostic only), ignored announcements, and countdown dispatch/skip events. Active timers also record a clock sample every 10 real seconds and a start-reached event. The ring buffer retains 160 lines.

```text
Start ET 22:30  in 123s
```

After the start time passes, the plate switches to the configured in-progress background color and shows:

```text
討伐中
```

### ゲーム内での確認手順

1. 開発プラグインを再読み込みし、`/asmobplate` の Debug log に `timing v9` があることを確認します。
2. `Game ET` の時・分とゲーム標準時計のETを比較します。読み取り側の表示更新で境界付近に短い差が出ることはあります。
3. ゲームETの現在時刻から6 ET分程度先の時刻を、例えば `/echo ET1319` のように告知します。単独時でも解析と表示を確認できますが、標準カウントダウンはPT等の条件によって拒否されることがあります。
4. `receivedET` が受信時のETと一致し、`remainingRealSeconds` が秒を含む差から計算されていることを確認します。
5. PT内で残り約10秒の起動と開始ET到達を確認します。`Dump debug log` を押すと再検証用ログをファイルに保存できます。

### 自動検証

ゲーム本体に依存しない時計計算・日付またぎ・重複告知・起動タイミングの回帰テスト:

```powershell
dotnet run --project tests/AsMobPlate.Timing.Tests/AsMobPlate.Timing.Tests.csproj
dotnet build -c Debug
```

API定義の確認元: [ClientTime](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/System/Timer/ClientTime.cs)、[UIModule](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/UIModule.cs)、[Dalamud CommandManager](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/Command/CommandManager.cs)。ローカルに同梱されたDLLでも該当フィールド・メソッドを確認しています。

## Settings

Version 1.0.008 introduces a resizable tabbed settings window: Display, Alerts, Start time, Appearance, Log, and Info. The EN / JP / DE / FR selector stays above the tabs. The selected language is saved immediately; the initial language is JP. Existing settings retain their values.

Translations cover settings, units, tabs, fixed nameplate text, countdown text, and chat detection notices. Hunt names follow the game's language. User-authored TTS text and diagnostic log entries remain unchanged. Translation strings are maintained in `Localization/UiText.cs`; stable ImGui IDs preserve control identity when switching languages. Long field labels wrap above their controls, and each tab scrolls independently.

Open settings with:

```text
/asmobplate
```

The settings window controls rank visibility, display and notification distances, sound/TTS/chat/map flag behavior, ETA/start-time display, sizing, offsets, and colors.
