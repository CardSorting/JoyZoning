namespace JoyZoning.Cli;

public static class CompletionScript
{
    public const string Bash = """
        # jz bash completion — source with: eval "$(jz completion bash)"
        _jz_completions()
        {
            local cur prev opts
            COMPREPLY=()
            cur="${COMP_WORDS[COMP_CWORD]}"
            prev="${COMP_WORDS[COMP_CWORD-1]}"
            opts="--base-url --session --task --pretty --quiet --yes --approve-critical --field --tui --help"
            local cmds="health doctor lease heartbeat verify session task execution approval manager event hermes workspace config kanban tui completion raw"
            if [[ ${COMP_CWORD} -eq 1 ]]; then
                COMPREPLY=( $(compgen -W "${cmds}" -- "${cur}") )
                return
            fi
            case "${COMP_WORDS[1]}" in
                task)
                    COMPREPLY=( $(compgen -W "run verify complete fail recover list create dispatch lease heartbeat merge revoke" -- "${cur}") )
                    ;;
                session)
                    COMPREPLY=( $(compgen -W "list get create" -- "${cur}") )
                    ;;
                config)
                    COMPREPLY=( $(compgen -W "get explain" -- "${cur}") )
                    ;;
                hermes)
                    COMPREPLY=( $(compgen -W "health ensure dashboard ensure-dashboard tui" -- "${cur}") )
                    ;;
            esac
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${opts}" -- "${cur}") )
            fi
        }
        complete -F _jz_completions jz
        """;
}
