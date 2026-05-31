# Discord bot setup

The master server ships a Discord bot that lets players link their account, claim
a verification bounty, create tokenized-gold deposit and withdrawal requests, read
a help message, and chat with the in-game world from a bridged Discord channel.

A single Discord server can host **both** the staging and production master servers
at the same time because every command is prefixed:

| Environment | Command prefix | Example commands |
| ----------- | -------------- | ---------------- |
| Production  | `cap5`         | `/cap5-verify`, `/cap5-deposit`, `/cap5-withdraw`, `/cap5-help` |
| Staging     | `cap5stage`    | `/cap5stage-verify`, `/cap5stage-deposit`, `/cap5stage-withdraw`, `/cap5stage-help` |

> Run the staging and production master servers as **separate Discord applications**
> (each with its own bot token) inside the same Discord server. The command prefix
> keeps their slash commands from colliding.

## 1. Create the Discord application and bot

1. Open the [Discord Developer Portal](https://discord.com/developers/applications)
   and click **New Application**. Name it e.g. `Capitalism5` (and a second one
   `Capitalism5 Staging` for the staging environment).
2. Open the **Bot** tab and click **Add Bot**.
3. Click **Reset Token** and copy the token. This is the value for `DiscordBot:BotToken`.
   Keep it secret — store it as a deployment secret, never in source control.
4. Under **Privileged Gateway Intents**, enable **Message Content Intent**. The bot
   needs it to read messages in the bridged chat channel.

## 2. Invite the bot to your server

1. Open **OAuth2 → URL Generator**.
2. Select the `bot` and `applications.commands` scopes.
3. Under **Bot Permissions** select at least: **Send Messages**, **Read Message History**
   and **View Channel**.
4. Open the generated URL and authorize the bot for your Discord server.

## 3. Collect the ids

Enable **Developer Mode** in Discord (User Settings → Advanced), then right-click to
**Copy ID**:

- **Server (guild) id** → `DiscordBot:GuildId`. When set, slash commands register to
  that guild only and appear almost instantly. Leave it `0` to register commands
  globally (can take up to an hour to propagate).
- **Channel id** of the channel that mirrors the in-game chat → `DiscordBot:ChatChannelId`.
  Leave it `0` to disable the chat bridge.

## 4. Configure the master server

Set the `DiscordBot` section in `projects/MasterApi/appsettings.json` (or, preferably,
environment variables / secrets in production):

```json
"DiscordBot": {
  "Enabled": true,
  "BotToken": "your-bot-token",
  "CommandPrefix": "cap5",
  "GuildId": 123456789012345678,
  "ChatChannelId": 234567890123456789,
  "MasterFrontendUrl": "https://capitalism5.com",
  "DiscordInviteUrl": "https://discord.gg/PhHSxJvDn6",
  "DefaultNetwork": "ALGORAND",
  "LinkCodeLifetimeMinutes": 30
}
```

Using environment variables (note the double underscore):

```bash
DiscordBot__Enabled=true
DiscordBot__BotToken=your-bot-token
DiscordBot__CommandPrefix=cap5        # cap5stage on the staging master server
DiscordBot__GuildId=..
DiscordBot__ChatChannelId=..
```

When `Enabled` is `false` or `BotToken` is empty, the bot hosted service does not start.

## 5. Player workflow

1. The player signs in on the master frontend and generates a one-time link code
   (`requestDiscordLinkCode`). The code is valid for `LinkCodeLifetimeMinutes`.
2. In Discord the player runs `/cap5-verify code:<the code>`. The bot links the
   Discord account to the player account and awards the Discord verification bounty.
3. `/cap5-deposit network:ALGORAND` returns the deposit address and the **note** the
   player must include with the on-chain transfer (`CAP-<id>`).
4. `/cap5-withdraw amount:<grams> address:<destination> network:ALGORAND` debits the
   player's tokenized-gold balance and creates a withdrawal request.
5. `/cap5-help` posts the master frontend URL, the Discord invite, and a short guide.

## 6. Enable the two-way chat bridge

The bridge connects the Discord channel (`DiscordBot:ChatChannelId`) with the in-game
chat of every active game shard:

- **Discord → game:** when a message is posted in the bridged channel by a player whose
  Discord account is linked, the master server posts it to the in-game chat of each
  active game server on the player's behalf.
- **Game → Discord:** active game servers forward their in-game chat to the master
  server, which mirrors it into the Discord channel.

For the game shard to forward chat it must already be registered with the master server
(see `MasterServer` options) and have the chat bridge enabled in
`projects/Api/appsettings.json`:

```json
"MasterServer": {
  "RegistrationEnabled": true,
  "ChatBridgeEnabled": true,
  "ApiUrl": "https://master.capitalism5.com/graphql",
  "RegistrationKey": "shared-registration-key",
  "ServerKey": "this-shard-key"
}
```

A loop guard on both sides prevents a forwarded message from being echoed back.

## 7. Verify the setup

1. Start the master server with the bot enabled and check the logs for the bot login
   and the registered prefixed commands.
2. In Discord, run `/cap5-help` (or `/cap5stage-help`) and confirm the help text.
3. Link a test account and run `/cap5-verify`, `/cap5-deposit` and `/cap5-withdraw`.
4. Post a message in the bridged channel and confirm it appears in the in-game chat,
   and vice versa.
