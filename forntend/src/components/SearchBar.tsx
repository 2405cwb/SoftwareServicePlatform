interface SearchBarProps {
  value: string;

  placeholder: string;

  onChange: (value: string) => void;

  onClear: () => void;
}

function SearchBar({ value, placeholder, onChange, onClear }: SearchBarProps) {
  return (
    <div className="search-bar">
      <input
        type="text"
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />

      {value !== "" && (
        <button className="normal-button" onClick={onClear}>
          清空
        </button>
      )}
    </div>
  );
}

export default SearchBar;
