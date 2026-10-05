"use client";

import { Input } from "@/components/ui/input";
import { getSearchSuggestions, type CarSuggestion } from "@/lib/Carapi";
import React, { useEffect, useMemo, useRef, useState } from "react";

// Corpus is fetched once and cached at module scope so we never call the
// backend on every keystroke; typing filters the local corpus instead.
let corpusCache: CarSuggestion[] | null = null;
let corpusPromise: Promise<CarSuggestion[]> | null = null;

function loadCorpus(): Promise<CarSuggestion[]> {
  if (corpusCache) return Promise.resolve(corpusCache);
  if (!corpusPromise) {
    corpusPromise = getSearchSuggestions()
      .then((data) => {
        corpusCache = Array.isArray(data) ? data : [];
        return corpusCache;
      })
      .catch(() => {
        corpusPromise = null;
        return [] as CarSuggestion[];
      });
  }
  return corpusPromise;
}

interface SearchAutocompleteProps {
  value: string;
  onChange: (value: string) => void;
  onSubmit: (value: string) => void;
  placeholder?: string;
  className?: string;
  inputClassName?: string;
}

const MAX_SUGGESTIONS = 8;

const SearchAutocomplete: React.FC<SearchAutocompleteProps> = ({
  value,
  onChange,
  onSubmit,
  placeholder = "Search cars...",
  className,
  inputClassName,
}) => {
  const [suggestions, setSuggestions] = useState<CarSuggestion[]>([]);
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const wrapperRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let active = true;
    loadCorpus().then((data) => {
      if (active) setSuggestions(data);
    });
    return () => {
      active = false;
    };
  }, []);

  const filtered = useMemo(() => {
    const q = value.trim().toLowerCase();
    if (!q) return [];
    return suggestions
      .filter((s) => s.value.toLowerCase().includes(q))
      .sort((a, b) => {
        const aPrefix = a.value.toLowerCase().startsWith(q) ? 0 : 1;
        const bPrefix = b.value.toLowerCase().startsWith(q) ? 0 : 1;
        if (aPrefix !== bPrefix) return aPrefix - bPrefix;
        return a.value.localeCompare(b.value);
      })
      .slice(0, MAX_SUGGESTIONS);
  }, [value, suggestions]);

  useEffect(() => {
    const onClickOutside = (event: MouseEvent) => {
      if (
        wrapperRef.current &&
        !wrapperRef.current.contains(event.target as Node)
      ) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", onClickOutside);
    return () => document.removeEventListener("mousedown", onClickOutside);
  }, []);

  const choose = (next: string) => {
    onChange(next);
    onSubmit(next);
    setOpen(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      if (!open) setOpen(true);
      setActiveIndex((i) =>
        filtered.length ? (i + 1) % filtered.length : -1
      );
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((i) =>
        filtered.length ? (i - 1 + filtered.length) % filtered.length : -1
      );
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (open && filtered[activeIndex]) {
        choose(filtered[activeIndex].value);
      } else {
        onSubmit(value);
        setOpen(false);
      }
    } else if (e.key === "Escape") {
      setOpen(false);
    }
  };

  return (
    <div ref={wrapperRef} className={`relative ${className ?? ""}`}>
      <Input
        type="text"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
          setOpen(true);
          setActiveIndex(-1);
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        className={inputClassName}
        role="combobox"
        aria-expanded={open}
        aria-autocomplete="list"
        aria-controls="search-suggestions"
      />
      {open && filtered.length > 0 && (
        <ul
          id="search-suggestions"
          role="listbox"
          className="absolute left-0 right-0 top-full z-50 mt-1 max-h-72 overflow-y-auto rounded-md border bg-white py-1 shadow-lg"
        >
          {filtered.map((s, index) => (
            <li
              key={`${s.category}-${s.value}`}
              role="option"
              aria-selected={index === activeIndex}
              onMouseDown={(e) => {
                e.preventDefault();
                choose(s.value);
              }}
              onMouseEnter={() => setActiveIndex(index)}
              className={`flex cursor-pointer items-center justify-between px-3 py-2 text-sm ${
                index === activeIndex ? "bg-gray-100" : ""
              }`}
            >
              <span className="text-gray-900">{s.value}</span>
              <span className="ml-2 rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-500">
                {s.category}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};

export default SearchAutocomplete;
