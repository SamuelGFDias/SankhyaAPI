using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Como as respostas de erro do Sankhya viram exceção (#4, #5, #12, #14).
/// </summary>
public class ErrosFakeTests : IDisposable
{
    private const string Load = "CRUDServiceProvider.loadRecords";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    private ISankhyaRepository<Usuario> Usuarios() => _fake.CreateClient().Repository<Usuario>("Usuario");
    private ISankhyaRepository<Produto> Produtos() => _fake.CreateClient().Repository<Produto>("Produto");

    [Fact]
    public async Task Status_0_sem_mensagem_e_erro()
    {
        _fake.WithStatus(Load, "0");

        var ex = await Assert.ThrowsAsync<SankhyaException>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Equal("0", ex.Status);
        Assert.Contains("status 0", ex.Message);
    }

    [Fact]
    public async Task Status_1_com_mensagem_nao_e_erro()
    {
        _fake.WithStatus(Load, "1",
            $"<statusMessage><![CDATA[{SankhyaFakeServer.Base64("Aviso qualquer")}]]></statusMessage>" +
            "<responseBody><entities total=\"1\"><entity><f0>0</f0><f1>SUP</f1></entity></entities></responseBody>");

        Assert.Single(await Usuarios().FindAsync("1 = 1"));
    }

    [Fact]
    public async Task Status_3_vira_sessao_expirada()
    {
        _fake.WithStatus(Load, "3", $"<statusMessage><![CDATA[{SankhyaFakeServer.Base64("Não autorizado.")}]]></statusMessage>");

        var ex = await Assert.ThrowsAsync<SankhyaSessionExpiredException>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Equal("3", ex.Status);
        Assert.Contains("Não autorizado.", ex.Message);
    }

    [Fact]
    public async Task TsError_traz_a_mensagem_decodificada_e_o_codigo()
    {
        _fake.WithStatus(Load, "0",
            $"<tsError tsErrorCode=\"CORE_E00001\" tsErrorLevel=\"ERROR\"><statusMessage>{SankhyaFakeServer.Base64("Campo obrigatório: DESCRPROD")}</statusMessage></tsError>");

        var ex = await Assert.ThrowsAsync<SankhyaException>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Contains("Campo obrigatório: DESCRPROD", ex.Message);
        Assert.DoesNotContain("System.Byte[]", ex.Message);
        Assert.Equal("CORE_E00001", ex.TsErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é base64!")]
    public async Task Login_com_idusu_vazio_ou_invalido_nao_quebra(string idusu)
    {
        _fake.WithLoginRawUserId(idusu);
        _fake.WithEntities(Load, ["0", "SUP"]);

        Assert.Single(await Usuarios().FindAsync("1 = 1"));
    }

    [Fact]
    public async Task Erro_de_conversao_nao_expoe_o_valor_do_campo()
    {
        const string valor = "CPF 123.456.789-00";
        _fake.WithEntities(Load, ["1", "X", valor]);

        var ex = await Assert.ThrowsAsync<FormatException>(() => Produtos().FindAsync("1 = 1"));

        Assert.DoesNotContain(valor, ex.ToString());
        Assert.Contains("Preco", ex.Message);
        Assert.Equal(valor, ex.Data["Valor"]);
    }
}

/// <summary>
/// Classificação dos erros com as mensagens reais da homologação (docs/sankhya-protocolo.md, seção Erros) e as de
/// regra de negócio em trigger (SQL Server e Oracle).
/// </summary>
public class ClassificacaoDeErrosTests : IDisposable
{
    private const string Load = "CRUDServiceProvider.loadRecords";
    private const string Save = "CRUDServiceProvider.saveRecord";
    private const string Remove = "CRUDServiceProvider.removeRecord";
    private const string Query = "DbExplorerSP.executeQuery";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    [Theory]
    [InlineData("MobileLoginSP.login", "CORE_E01434", "Usuário/Senha inválido.", SankhyaErrorKind.AuthenticationFailed, 401, null)]
    [InlineData("MobileLoginSP.login", "CORE_E01428", "Usuário/Senha inválido.", SankhyaErrorKind.AuthenticationFailed, 401, null)]
    [InlineData(Load, "CORE_E04064", "Descritor do campo 'CAMPOXYZ' inválido.", SankhyaErrorKind.InvalidRequest, 400, "CAMPOXYZ")]
    [InlineData(Load, "CORE_E03405", "Relacionamento: 'Foo' não encontrado em: 'Cidade'", SankhyaErrorKind.InvalidRequest, 400, null)]
    [InlineData(Load, null, "Não foi encontrado objeto de acesso a dados para este BMP: mge-dwf:EntidadeXyz", SankhyaErrorKind.InvalidRequest, 400, null)]
    [InlineData(Load, null, "Sintaxe incorreta próxima a '='.", SankhyaErrorKind.InvalidRequest, 400, null)]
    [InlineData(Load, null, "Parametros informados insuficientes para o critério de consulta.", SankhyaErrorKind.InvalidRequest, 400, null)]
    [InlineData(Load, "CORE_E02271", "Erro de conversão para número: abc", SankhyaErrorKind.InvalidValue, 422, null)]
    [InlineData(Load, "CORE_E07537", "Necessario habilitar o parametro 'LOGTABOPER'.", SankhyaErrorKind.NotConfigured, 501, null)]
    [InlineData(Save, null, "Violação da restrição PRIMARY KEY 'PK_AD_TESTE'. Não é possível inserir a chave duplicada no objeto 'sankhya.AD_TESTE'. O valor de chave duplicada é (777001).", SankhyaErrorKind.DuplicateKey, 409, null)]
    [InlineData(Save, null, "Propriedade 'AD_TESTE.TEXTO' com largura acima do limite: (150 > 100)", SankhyaErrorKind.InvalidValue, 422, "TEXTO")]
    [InlineData(Save, null, "Erro de conversão de valor para o campo 'INTEIRO': abc", SankhyaErrorKind.InvalidValue, 422, "INTEIRO")]
    [InlineData(Save, null, "Campo não existe: AD_TESTE->CAMPOXYZ", SankhyaErrorKind.InvalidRequest, 400, "CAMPOXYZ")]
    [InlineData(Save, null, "A instrução INSERT conflitou com a restrição do FOREIGN KEY \"FK_X\".", SankhyaErrorKind.ConstraintViolation, 409, null)]
    [InlineData(Save, null, "ORA-02291: integrity constraint (SANKHYA.FK_X) violated - parent key not found", SankhyaErrorKind.ConstraintViolation, 409, null)]
    [InlineData(Save, null, "O serviço foi cancelado por situação de concorrência. Essa mesma sessão HTTP fez a requisição duas vezes simultaneamente.", SankhyaErrorKind.Concurrency, 409, null)]
    [InlineData(Query, null, "Nome de objeto 'TABELA_XYZ' inválido.", SankhyaErrorKind.InvalidRequest, 400, null)]
    [InlineData(Query, null, "Permitido apenas a execução de consultas, a instrução possui 'UPDATE', 'DELETE' ou 'INSERT' e tais comandos não são permitidos.", SankhyaErrorKind.InvalidRequest, 400, null)]
    [InlineData(Query, null, "Erro de divisão por zero.", SankhyaErrorKind.QueryFailed, 400, null)]
    [InlineData("Outro.servico", null, "Algo inesperado", SankhyaErrorKind.Unknown, 502, null)]
    [InlineData(Remove, null, "Contato não existe: PK[4,210]", SankhyaErrorKind.NotFound, 404, null)]
    [InlineData(Save, null, "Produto não existe: PK[999999999]", SankhyaErrorKind.InvalidValue, 422, null)]
    [InlineData(Save, null, "Não é possível inserir quantidade ímpar para o produto: - 108R00961", SankhyaErrorKind.BusinessRule, 422, null)]
    [InlineData(Save, null, "A instrução UPDATE conflitou com a restrição do FOREIGN KEY \"FK_TGFITE_TGFLOC\". column 'CODLOCAL'.", SankhyaErrorKind.ConstraintViolation, 409, "CODLOCAL")]
    [InlineData(Save, "CORE_E00912", "Empresa não encontrada", SankhyaErrorKind.InvalidValue, 422, null)]
    [InlineData(Save, "CORE_E00916", "TOP não encontrada.", SankhyaErrorKind.InvalidValue, 422, null)]
    [InlineData(Save, null, "Data truncation", SankhyaErrorKind.InvalidValue, 422, null)]
    [InlineData(Save, null, "Ano inferior ao limite permitido, veja o parâmetro de Limite Inferior para Ano.", SankhyaErrorKind.BusinessRule, 422, null)]
    [InlineData(Save, null, "O preenchimento do endereço do contato é obrigatório.", SankhyaErrorKind.BusinessRule, 422, null)]
    [InlineData(Query, null, "HttpServiceBroker: Nenhum provedor foi encontrado para o serviço 'CACSP.incluirNota'.", SankhyaErrorKind.InvalidRequest, 400, null)]
    public void Classifica_pelo_codigo_e_pela_mensagem(string servico, string? codigo, string mensagem, SankhyaErrorKind tipo, int http, string? campo)
    {
        (SankhyaErrorKind kind, string? field) = SankhyaErrorClassifier.Classify(servico, "0", mensagem, codigo);

        Assert.Equal(tipo, kind);
        Assert.Equal(http, (int)SankhyaException.SuggestedStatusFor(kind));
        Assert.Equal(campo, field);
    }

    [Fact]
    public async Task Regra_de_trigger_do_SQL_Server_vira_BusinessRule_com_a_mensagem_limpa()
    {
        _fake.WithError(Save, "Pedido bloqueado: cliente com títulos vencidos. A transação foi encerrada no gatilho. O lote foi anulado.");

        var ex = await Assert.ThrowsAsync<SankhyaBusinessRuleException>(
            () => _fake.CreateClient().Repository<Produto>().CreateAsync(new Produto { DescrProd = "X" }));

        Assert.Equal("Pedido bloqueado: cliente com títulos vencidos.", ex.Message);
        Assert.Equal(SankhyaErrorKind.BusinessRule, ex.Kind);
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, ex.SuggestedHttpStatus);
    }

    [Fact]
    public async Task Regra_de_trigger_do_Oracle_vira_BusinessRule_sem_codigo_nem_pilha()
    {
        _fake.WithError(Remove, "ORA-20101: Não é permitido excluir produto com movimento\nORA-06512: at \"SANKHYA.TRG_TGFPRO\", line 12\nORA-04088: error during execution of trigger");

        var ex = await Assert.ThrowsAsync<SankhyaBusinessRuleException>(
            () => _fake.CreateClient().Repository<Produto>().DeleteAsync(new Produto { CodProd = 1 }));

        Assert.Equal("Não é permitido excluir produto com movimento", ex.Message);
    }

    [Fact]
    public void Prefixo_SQL_50001_do_RAISERROR_sai_da_mensagem()
    {
        Assert.Equal("Ano inferior ao limite permitido. Nota de Nro Único: 1.",
            SankhyaErrorClassifier.Clean("SQL-50001 Ano inferior ao limite permitido. Nota de Nro Único: 1."));
    }

    [Fact]
    public async Task Gravacao_recusada_sem_mensagem_e_Unknown_explicado_e_nao_regra_de_negocio()
    {
        _fake.WithStatus(Save, "0", "<statusMessage></statusMessage>");

        var ex = await Assert.ThrowsAnyAsync<SankhyaException>(
            () => _fake.CreateClient().Repository<Produto>().CreateAsync(new Produto { DescrProd = "X" }));

        Assert.Equal(SankhyaErrorKind.Unknown, ex.Kind);
        Assert.IsNotType<SankhyaBusinessRuleException>(ex);
        Assert.Contains("sem mensagem", ex.Message);
    }

    [Fact]
    public async Task ToProblem_gera_um_ProblemDetails()
    {
        _fake.WithError(Save, "Violação da restrição PRIMARY KEY 'PK_X'. Não é possível inserir a chave duplicada.");

        var ex = await Assert.ThrowsAnyAsync<SankhyaException>(
            () => _fake.CreateClient().Repository<Produto>().CreateAsync(new Produto { DescrProd = "X" }));
        SankhyaProblem problema = ex.ToProblem();

        Assert.Equal(409, problema.Status);
        Assert.Equal("urn:sankhya:error:DuplicateKey", problema.Type);
        Assert.Equal(SankhyaErrorKind.DuplicateKey, problema.Kind);
        Assert.Equal(Save, problema.Service);
    }

    [Fact]
    public async Task Update_de_chave_inexistente_e_barrado_antes_de_gravar()
    {
        _fake.WithRawXml(Load, "<responseBody><entities total=\"0\" /></responseBody>");

        var ex = await Assert.ThrowsAsync<SankhyaNotFoundException>(
            () => _fake.CreateClient().Repository<Produto>().UpdateAsync(new Produto { CodProd = 10, DescrProd = "X" }));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, ex.SuggestedHttpStatus);
        Assert.Empty(_fake.Calls(Save));
        string consulta = _fake.Calls(Load).Single().RequestMessage!.Body!;
        Assert.Contains("<expression>(this.CODPROD = ?)</expression>", consulta);
        Assert.Contains("<field name=\"CODPROD\" />", consulta);
    }

    [Fact]
    public async Task Update_de_chave_existente_confere_e_grava()
    {
        _fake.WithEntities(Load, ["10"]);
        _fake.WithEntities(Save, ["10", "X"]);

        await _fake.CreateClient().Repository<Produto>().UpdateManyAsync([new Produto { CodProd = 10, DescrProd = "X" }]);

        Assert.Single(_fake.Calls(Load));
        Assert.Single(_fake.Calls(Save));
    }

    [Fact]
    public async Task Verificacao_de_existencia_pode_ser_desligada()
    {
        _fake.WithEntities(Save, ["10", "X"]);

        await _fake.CreateClient(configure: o => o.VerifyExistenceOnUpdate = false).Repository<Produto>().UpdateAsync(new Produto { CodProd = 10, DescrProd = "X" });

        Assert.Empty(_fake.Calls(Load));
    }
}
