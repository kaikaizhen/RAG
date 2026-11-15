import { KeyRound } from 'lucide-react';
import { TokenManager } from '@/components/TokenManager';
import { PageHeader } from '@/components/ui';
import { listMcpTokens } from '@/lib/api';

export default async function TokensPage() {
  const { tokens } = await listMcpTokens();
  const mcpUrl = process.env.MCP_PUBLIC_URL ?? 'https://你的筆記服務網域/mcp';

  return (
    <>
      <PageHeader title="MCP Token" icon={<KeyRound size={18} strokeWidth={1.8} aria-hidden="true" />}>
        讓你的 Claude 讀寫你的筆記。每組 token 只碰得到你自己的筆記，弄丟了就撤銷再產生一組。
      </PageHeader>
      <TokenManager initialTokens={tokens} mcpUrl={mcpUrl} />

      <section className="token-help">
        <h2>怎麼使用</h2>
        <ol>
          <li>在上面替這組 token 取個名字，按「產生 token」。</li>
          <li>用 Claude Code 的話，複製頁面給的指令，貼到終端機執行。</li>
          <li>接上後跟 Claude 說「幫我把這次改動記下來」，或問它「我之前為什麼這樣改」。</li>
        </ol>
        <h3>用其他支援 MCP 的工具</h3>
        <dl>
          <dt>連線方式</dt>
          <dd>Streamable HTTP</dd>
          <dt>網址</dt>
          <dd>
            <code>{mcpUrl}</code>
          </dd>
          <dt>Header</dt>
          <dd>
            <code>Authorization: Bearer 你的 token</code>
          </dd>
        </dl>
        <p className="token-warning">
          token 等同你的筆記權限，拿到的人可以讀、改、刪你的筆記。不要貼給別人，也不要放進 git。外流了就在這頁撤銷。
        </p>
      </section>
    </>
  );
}
