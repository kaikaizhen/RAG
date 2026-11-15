'use client';

import { Check, ChevronDown } from 'lucide-react';
import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';

export interface SelectOption {
  value: string;
  label: string;
}

/**
 * 外觀可以自訂的下拉選單，選了就直接套用篩選。
 * 值放在 hidden input 裡，所以仍然是一般的 GET 表單。
 */
export function Select({
  name,
  label,
  value,
  options,
}: {
  name: string;
  label: string;
  value: string;
  options: SelectOption[];
}) {
  const id = useId();
  const root = useRef<HTMLDivElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState(value);
  const selectedIndex = Math.max(options.findIndex((option) => option.value === selected), 0);
  const [active, setActive] = useState(selectedIndex);

  useEffect(() => {
    if (!open) return;
    const close = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('pointerdown', close);
    return () => document.removeEventListener('pointerdown', close);
  }, [open]);

  useEffect(() => {
    if (open) document.getElementById(`${id}-${active}`)?.scrollIntoView({ block: 'nearest' });
  }, [open, active, id]);

  const show = () => {
    setActive(selectedIndex);
    setOpen(true);
  };

  const choose = (index: number) => {
    const option = options[index];
    setOpen(false);
    if (!option || option.value === selected) return;
    setSelected(option.value);
    // 先寫進 input 再送出，不用等 React 更新畫面
    if (input.current) input.current.value = option.value;
    input.current?.form?.requestSubmit();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'Tab') {
      setOpen(false);
      return;
    }
    if (!open) {
      if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key)) {
        event.preventDefault();
        show();
      }
      return;
    }
    const last = options.length - 1;
    const moves: Record<string, () => void> = {
      ArrowDown: () => setActive(Math.min(active + 1, last)),
      ArrowUp: () => setActive(Math.max(active - 1, 0)),
      Home: () => setActive(0),
      End: () => setActive(last),
      Enter: () => choose(active),
      ' ': () => choose(active),
      Escape: () => setOpen(false),
    };
    const move = moves[event.key];
    if (move) {
      event.preventDefault();
      move();
    }
  };

  return (
    <div className="select" ref={root}>
      <input ref={input} type="hidden" name={name} defaultValue={value} />
      <button
        type="button"
        role="combobox"
        className="select-trigger"
        aria-label={label}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={`${id}-list`}
        aria-activedescendant={open ? `${id}-${active}` : undefined}
        onClick={() => (open ? setOpen(false) : show())}
        onKeyDown={onKeyDown}
      >
        <span>{options[selectedIndex]?.label}</span>
        <ChevronDown size={17} aria-hidden="true" />
      </button>
      {open && (
        <ul className="select-list" id={`${id}-list`} role="listbox" aria-label={label}>
          {options.map((option, index) => (
            <li
              key={option.value}
              id={`${id}-${index}`}
              role="option"
              aria-selected={index === selectedIndex}
              className={index === active ? 'is-active' : undefined}
              onPointerEnter={() => setActive(index)}
              // 不讓按鈕失去焦點
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => choose(index)}
            >
              <span>{option.label}</span>
              {index === selectedIndex && <Check size={16} aria-hidden="true" />}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
