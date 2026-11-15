import { Compass } from 'lucide-react';
import Link from 'next/link';
import { KnowledgeLoop } from '@/components/KnowledgeLoop';
import { PageHeader } from '@/components/ui';

const TOOLS = [
  ['add_entry', '記一筆程式改動', '「幫我把這次改動記下來」'],
  ['search_entries', '找過去的改動', '「我之前為什麼這樣改？」'],
  ['save_concept', '存一張觀念卡', '「把料號這個觀念記起來」'],
  ['save_article', '寫一篇學習筆記', '「把 ERP 怎麼串起來整理成筆記」'],
  ['update_project_rules', '更新模組規則', '「預約規則改成三天前」'],
  ['list_knowledge_gaps', '看問答答不出來的問題', '「我有哪些問題還沒答案？」'],
];

export default function GuidePage() {
  return (
    <>
      <PageHeader title="使用指南" icon={<Compass size={18} strokeWidth={1.8} aria-hidden="true" />}>
        筆記負責寫，問答負責問。答不出來的問題會回到筆記，下次就答得出來。
      </PageHeader>

      <KnowledgeLoop />

      <section className="guide-section">
        <h2>兩個網站的分工</h2>
        <div className="guide-pair">
          <div>
            <h3>學習筆記</h3>
            <p>Claude 透過 MCP 寫進來的紀錄、筆記、規則和觀念卡。網頁只能看，要改就跟 Claude 說。</p>
          </div>
          <div>
            <h3>問答系統</h3>
            <p>用同一組帳號登入，用自然語言問。回答下方的來源標籤可以點回原本的筆記。</p>
          </div>
        </div>
      </section>

      <section className="guide-section">
        <h2>開始使用</h2>
        <ol className="guide-steps">
          <li>
            到 <Link href="/tokens">Token 頁</Link>產生 token，把頁面給的指令貼到終端機。
          </li>
          <li>寫完程式跟 Claude 說「幫我把這次改動記下來」。</li>
          <li>
            到 <Link href="/sharing">分享範圍</Link>，把想問的專案設成「我的問答」。
          </li>
          <li>回問答系統提問。答不出來就按「交給筆記」，Claude 下次會提議補上。</li>
        </ol>
      </section>

      <section className="guide-section">
        <h2>可以跟 Claude 說什麼</h2>
        <div className="guide-table" role="table" aria-label="MCP 工具">
          {TOOLS.map(([tool, what, say]) => (
            <div role="row" key={tool}>
              <code role="cell">{tool}</code>
              <span role="cell">{what}</span>
              <em role="cell">{say}</em>
            </div>
          ))}
        </div>
      </section>

      <section className="guide-section">
        <h2>誰看得到你的筆記</h2>
        <div className="guide-table" role="table" aria-label="分享範圍">
          <div role="row">
            <strong role="cell">只留在筆記</strong>
            <span role="cell">只有你看得到，不會送到問答系統。新專案的預設值。</span>
          </div>
          <div role="row">
            <strong role="cell">我的問答</strong>
            <span role="cell">同步到你自己的空間，只有你問得到。</span>
          </div>
          <div role="row">
            <strong role="cell">公開</strong>
            <span role="cell">所有登入的人都看得到、問得到，回答會標示來自公開知識。</span>
          </div>
        </div>
        <p className="guide-note">私人是指其他使用者看不到。管理主機的人技術上仍能存取資料庫，公司機密請依公司規定處理。</p>
      </section>
    </>
  );
}
