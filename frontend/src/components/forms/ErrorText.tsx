export function ErrorText({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <p className="text-sm text-red-600" role="alert">
      {message}
    </p>
  );
}
