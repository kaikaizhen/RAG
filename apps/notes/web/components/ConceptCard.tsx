import { Code2, HelpCircle, Lightbulb, MapPin } from 'lucide-react';
import Link from 'next/link';
import type { Concept } from '@/lib/api';
import { ConfidenceBadge, Field } from './ui';

export function ConceptCard({ concept }: { concept: Concept }) {
  const initial = Array.from(concept.term)[0]?.toUpperCase();
  return (
    <article className="concept" id={`concept-${concept.id}`}>
      <span className={`concept-mark mark-${concept.confidence}`} aria-hidden="true">
        {initial}
      </span>
      <header className="concept-side">
        <h2>{concept.term}</h2>
        <ConfidenceBadge confidence={concept.confidence} confirmedBy={concept.confirmed_by} />
        {concept.project && <span className="concept-project">{concept.project}</span>}
      </header>
      <div className="concept-body">
        <p className="lead">{concept.plain_explanation}</p>
        <dl>
          <Field label="為什麼需要" icon={<Lightbulb size={13} strokeWidth={1.9} aria-hidden="true" />}>{concept.why_needed}</Field>
          <Field label="實際例子" icon={<Code2 size={13} strokeWidth={1.9} aria-hidden="true" />}>{concept.example}</Field>
          <Field label="系統位置" icon={<MapPin size={13} strokeWidth={1.9} aria-hidden="true" />}>{concept.where_in_system}</Field>
        </dl>
        {concept.open_questions && (
          <aside className="note">
            <strong>
              <HelpCircle size={13} strokeWidth={1.9} aria-hidden="true" />
              待釐清
            </strong>
            <p>{concept.open_questions}</p>
          </aside>
        )}
        {concept.related.length > 0 && (
          <p className="related">
            <span>相關觀念</span>
            {concept.related.map((term) => (
              <Link key={term} className="term-chip" href={`/concepts?query=${encodeURIComponent(term)}`}>
                {term}
              </Link>
            ))}
          </p>
        )}
      </div>
    </article>
  );
}
