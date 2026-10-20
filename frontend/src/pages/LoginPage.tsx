import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { TextField } from '../components/forms/TextField';
import { Button } from '../components/forms/Button';
import { ErrorText } from '../components/forms/ErrorText';

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      navigate('/');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-canvas">
      <form onSubmit={handleSubmit} className="bg-white border border-line rounded-lg shadow-sm p-8 w-full max-w-sm space-y-4">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-primary-600" aria-hidden="true" />
          <h1 className="text-lg font-semibold text-ink">Connexion</h1>
        </div>
        <ErrorText message={error} />
        <TextField label="Email" type="email" required value={email} onChange={setEmail} />
        <TextField label="Mot de passe" type="password" required value={password} onChange={setPassword} />
        <Button type="submit" disabled={submitting} className="w-full">
          {submitting ? 'Connexion...' : 'Se connecter'}
        </Button>
        <p className="text-sm text-gray-600 text-center">
          Pas de compte ? <Link to="/register" className="text-primary-600 hover:text-primary-700 underline">S'inscrire</Link>
        </p>
      </form>
    </div>
  );
}
