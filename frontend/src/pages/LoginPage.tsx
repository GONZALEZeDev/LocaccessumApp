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
    <div className="min-h-screen flex items-center justify-center bg-primary-50">
      <form onSubmit={handleSubmit} className="bg-white rounded-lg shadow p-8 w-full max-w-sm space-y-4">
        <h1 className="text-xl font-semibold text-primary-800">Connexion</h1>
        <ErrorText message={error} />
        <TextField label="Email" type="email" required value={email} onChange={setEmail} />
        <TextField label="Mot de passe" type="password" required value={password} onChange={setPassword} />
        <Button type="submit" disabled={submitting} className="w-full">
          {submitting ? 'Connexion...' : 'Se connecter'}
        </Button>
        <p className="text-sm text-gray-600 text-center">
          Pas de compte ? <Link to="/register" className="text-primary-700 underline">S'inscrire</Link>
        </p>
      </form>
    </div>
  );
}
