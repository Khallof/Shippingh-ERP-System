import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import type { CurrencyDto } from '@/api/generated';

/**
 * Step 9: money screens show ONE currency at a time, in its own amounts (dollars stay dollars, dinars stay
 * dinars): nothing is converted or added across currencies. The base currency is selected first.
 */
export function useReportCurrency() {
  const currencies = useQuery({
    queryKey: ['/currencies', 'report-currency'],
    queryFn: () => api.list<CurrencyDto>('/currencies', { isActive: true, pageNumber: 1, pageSize: 100, sortBy: 'code', sortDirection: 'asc' }),
    staleTime: 5 * 60_000
  });
  const [chosen, setChosen] = useState<number | null>(null);
  const list = currencies.data?.items ?? [];
  const currencyId = chosen ?? (list.find((c) => c.isBaseCurrency) ?? list[0])?.id ?? null;
  const code = list.find((c) => c.id === currencyId)?.code ?? '';
  return { list, currencyId, code, setCurrencyId: setChosen, loading: currencies.isLoading, error: currencies.error };
}
