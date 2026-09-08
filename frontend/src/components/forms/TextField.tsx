interface TextFieldProps {
  label: string;
  type?: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  placeholder?: string;
}

export function TextField({ label, type = 'text', value, onChange, required, placeholder }: TextFieldProps) {
  return (
    <label className="block text-sm text-gray-700">
      {label}
      <input
        type={type}
        required={required}
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-400"
      />
    </label>
  );
}
