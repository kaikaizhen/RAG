import { ArrowRight } from 'lucide-react';
import Link from 'next/link';

/** 第一次登入、還沒有任何筆記時，告訴使用者怎麼開始。 */
export function Onboarding() {
  return (
    <section className="onboarding" aria-labelledby="onboarding-title">
      <h2 id="onboarding-title">三步開始記筆記</h2>
      <ol>
        <li>
          <strong>產生 token</strong>
          <p>這是讓你的 Claude 進來寫筆記的鑰匙，只碰得到你自己的筆記。</p>
          <Link href="/tokens" className="primary-button onboarding-action">
            前往產生
            <ArrowRight size={16} aria-hidden="true" />
          </Link>
        </li>
        <li>
          <strong>接到 Claude</strong>
          <p>產生後頁面會給你一行指令，貼到終端機執行就接上了。</p>
        </li>
        <li>
          <strong>跟 Claude 說要記什麼</strong>
          <p>改完程式後跟它說「幫我把這次改動記下來」，筆記就會出現在這裡。</p>
        </li>
      </ol>
    </section>
  );
}
