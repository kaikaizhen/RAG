'use client';

import { Eye, Layers, StepForward } from 'lucide-react';
import { useState } from 'react';
import type { Confidence } from '@/lib/api';
import { ConfidenceBadge } from './ui';

export interface FlashCard {
  id: number;
  term: string;
  plain_explanation: string;
  confidence: Confidence;
  confirmed_by: string | null;
}

/** 起始位置由伺服器決定，避免伺服器和瀏覽器畫出不同的卡片。 */
export function FlashCards({ cards, start }: { cards: FlashCard[]; start: number }) {
  const [index, setIndex] = useState(start);
  const [revealed, setRevealed] = useState(false);
  const card = cards[index];
  if (!card) return null;

  const next = () => {
    setRevealed(false);
    setIndex((index + 1) % cards.length);
  };

  return (
    <div className="review">
      <div className="review-progress" aria-hidden="true">
        <span style={{ width: `${((index + 1) / cards.length) * 100}%` }} />
      </div>
      <div className="review-kicker">
        <Layers size={13} strokeWidth={1.9} aria-hidden="true" />
        第 {index + 1} 張，共 {cards.length} 張
      </div>
      <div className="review-term">{card.term}</div>
      <div aria-live="polite">
        {revealed && (
          <>
            <p className="review-answer">{card.plain_explanation}</p>
            <ConfidenceBadge confidence={card.confidence} confirmedBy={card.confirmed_by} />
          </>
        )}
      </div>
      <div className="review-actions">
        {!revealed && (
          <button type="button" className="primary-button" onClick={() => setRevealed(true)}>
            <Eye size={14} aria-hidden="true" />
            顯示解釋
          </button>
        )}
        {cards.length > 1 && (
          <button type="button" className="ghost-button" onClick={next}>
            <StepForward size={14} aria-hidden="true" />
            下一個
          </button>
        )}
      </div>
    </div>
  );
}
