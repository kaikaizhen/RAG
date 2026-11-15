import { Bot, CircleHelp, MessageCircle, NotebookPen } from 'lucide-react';

const STEPS = [
  { icon: Bot, title: 'Claude 寫筆記', text: '透過 MCP，用你的 token' },
  { icon: NotebookPen, title: '筆記保存', text: '你決定誰問得到' },
  { icon: MessageCircle, title: '問答回答', text: '附上來源和可信度' },
  { icon: CircleHelp, title: '答不出來', text: '問題回到筆記，等 Claude 補上' },
];

/** 知識循環圖：光點沿著四個步驟繞圈，表示問不到的問題會變成下一筆筆記。 */
export function KnowledgeLoop() {
  return (
    <ol className="loop" aria-label="筆記和問答的循環">
      {STEPS.map(({ icon: Icon, title, text }) => (
        <li key={title} className="loop-step">
          <span className="loop-icon">
            <Icon size={18} strokeWidth={1.8} aria-hidden="true" />
          </span>
          <strong>{title}</strong>
          <small>{text}</small>
        </li>
      ))}
    </ol>
  );
}
