import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'danger';

const variantClasses: Record<Variant, string> = {
  primary: 'bg-primary-500 text-white hover:bg-primary-600',
  secondary: 'bg-gray-100 text-gray-800 hover:bg-gray-200',
  danger: 'bg-red-500 text-white hover:bg-red-600',
};

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
}

export function Button({ variant = 'primary', className = '', ...rest }: ButtonProps) {
  return (
    <button
      className={`rounded px-4 py-2 font-medium transition-colors disabled:opacity-50 ${variantClasses[variant]} ${className}`}
      {...rest}
    />
  );
}
