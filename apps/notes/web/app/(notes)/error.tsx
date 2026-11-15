'use client';

export default function ErrorPage({ reset }: { reset: () => void }) {
  return (
    <div className="empty" role="alert">
      <h1 className="error-title">筆記讀取失敗</h1>
      <p>後端可能暫時連不上。稍等一下再試，還是不行就到主機看服務有沒有在跑。</p>
      <button type="button" onClick={reset}>
        再試一次
      </button>
    </div>
  );
}
